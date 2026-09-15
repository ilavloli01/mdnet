import Markdoc, { Tag, type Config, type Node, type RenderableTreeNode, type Schema } from "@markdoc/markdoc";
import { slug } from "./context";

const { nodes } = Markdoc;

/** Labels written by mdnet as `**Label**: text` rows. */
const ROW_LABELS = new Set(["Returns", "Value", "Throws", "See also"]);

const isTag = (node: RenderableTreeNode | undefined, name?: string): node is Tag =>
  Tag.isTag(node) && (name === undefined || node.name === name);

export function textOf(node: RenderableTreeNode | RenderableTreeNode[] | undefined): string {
  if (node === undefined || node === null) return "";
  if (Array.isArray(node)) return node.map(textOf).join("");
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (isTag(node)) {
    if (node.name === "InlineCode") return String(node.attributes.content ?? "");
    return textOf(node.children);
  }
  return "";
}

function transformOne(node: Node, config: Config): RenderableTreeNode[] {
  const result = Markdoc.transform(node, config);
  return Array.isArray(result) ? result : [result];
}

/** `**Label**: rest` → label and rest (leading colon removed). */
function splitLabel(children: RenderableTreeNode[]): { label: string; rest: RenderableTreeNode[] } | null {
  const [first, ...rest] = children;
  if (!isTag(first, "Strong")) return null;
  const label = textOf(first).trim();
  const out = [...rest];
  if (typeof out[0] === "string") {
    out[0] = out[0].replace(/^\s*:\s*/, "");
    if (out[0] === "") out.shift();
  }
  return { label, rest: out };
}

function unwrapParagraph(children: RenderableTreeNode[]): RenderableTreeNode[] {
  return children.length === 1 && isTag(children[0], "Paragraph") ? children[0].children : children;
}

/**
 * Turns doc-comment Markdown into semantic tags: parameter lists, labeled rows (Returns/Value/Throws),
 * remarks and code blocks. Everything else keeps its default rendering and order.
 */
function classify(children: RenderableTreeNode[]): RenderableTreeNode[] {
  return children.map((child) => {
    // "* [Type](Type.md): summary" lists (package index, nested types) become a type listing.
    if (isTag(child, "List") && child.children.length > 0) {
      const rows = child.children.map((item) => (isTag(item) ? unwrapParagraph(item.children) : []));
      if (rows.every((row) => isTag(row[0], "DocLink"))) {
        return new Tag(
          "TypeList",
          {},
          rows.map(([link, ...rest]) => {
            const summary = [...rest];
            if (typeof summary[0] === "string") summary[0] = summary[0].replace(/^\s*:\s*/, "");
            const typeLink = link as Tag;
            return new Tag("TypeListItem", {}, [
              new Tag("DocLink", { ...typeLink.attributes, variant: "type" }, typeLink.children),
              new Tag("Paragraph", { tone: "muted" }, summary),
            ]);
          }),
        );
      }
    }

    if (isTag(child, "List") && child.children.length > 0) {
      const params = child.children.map((item) => (isTag(item) ? splitLabel(unwrapParagraph(item.children)) : null));
      if (params.every((p) => p !== null)) {
        return new Tag(
          "Params",
          {},
          params.map((p) => new Tag("Param", { name: p!.label }, p!.rest)),
        );
      }
    }

    if (isTag(child, "Paragraph")) {
      const labeled = splitLabel(child.children);
      if (labeled && ROW_LABELS.has(labeled.label)) {
        return new Tag("Row", { label: labeled.label.toLowerCase() }, labeled.rest);
      }
      // "**Throws** [X](..): text" has no colon right after the label.
      const [first, ...rest] = child.children;
      if (isTag(first, "Strong") && textOf(first).trim() === "Throws") {
        const targetIndex = rest.findIndex((r) => isTag(r));
        const target = rest[targetIndex];
        const after = rest.slice(targetIndex + 1);
        if (typeof after[0] === "string") after[0] = after[0].replace(/^\s*:\s*/, "");
        return new Tag("Row", { label: "throws" }, [new Tag("RowTarget", {}, target ? [target] : []), ...after]);
      }
    }

    if (isTag(child, "Blockquote")) {
      const [firstParagraph, ...others] = child.children;
      if (isTag(firstParagraph, "Paragraph")) {
        const labeled = splitLabel(firstParagraph.children);
        if (labeled?.label === "Remarks") {
          return new Tag("Remarks", {}, [new Tag("Paragraph", { tone: "muted" }, labeled.rest), ...others]);
        }
      }
    }

    return child;
  });
}

function isBadgeParagraph(tag: RenderableTreeNode | undefined): tag is Tag {
  return (
    isTag(tag, "Paragraph") &&
    tag.children.some((c) => isTag(c, "InlineCode")) &&
    tag.children.every((c) => isTag(c, "InlineCode") || (typeof c === "string" && c.trim() === ""))
  );
}

const member: Schema = {
  render: "Member",
  attributes: { id: { type: String } },
  transform(node, config) {
    const [first, ...rest] = node.children;
    const hasSignature = first?.type === "fence";
    const body = (hasSignature ? rest : node.children).flatMap((child) => transformOne(child, config));
    return new Tag(
      "Member",
      { id: node.attributes.id, signature: hasSignature ? first!.attributes.content.replace(/\n$/, "") : undefined },
      classify(body),
    );
  },
};

/** Embedded README (package or namespace): plain prose, no doc-comment classification. */
const readme: Schema = {
  render: "Readme",
  transform(node, config) {
    return new Tag("Readme", {}, node.transformChildren(config));
  },
};

/**
 * Page structure: header (h1, kind badges, declaration, doc comment) followed by one Section per h2.
 */
const document: Schema = {
  ...nodes.document,
  transform(node, config) {
    const children = node.children;
    const firstSection = children.findIndex((c) => c.type === "heading" && c.attributes.level === 2);
    const headerNodes = firstSection < 0 ? children : children.slice(0, firstSection);
    const sectionNodes = firstSection < 0 ? [] : children.slice(firstSection);

    let title = "";
    let badges: string[] = [];
    let declaration: string | undefined;
    const headerBody: RenderableTreeNode[] = [];
    headerNodes.forEach((child, index) => {
      if (index === 0 && child.type === "heading" && child.attributes.level === 1) {
        title = textOf(transformOne(child, config));
        return;
      }
      if (declaration === undefined && badges.length > 0 && child.type === "fence" && headerBody.length === 0) {
        declaration = String(child.attributes.content).replace(/\n$/, "");
        return;
      }
      const rendered = transformOne(child, config);
      if (badges.length === 0 && headerBody.length === 0 && isBadgeParagraph(rendered[0])) {
        badges = rendered[0].children.filter((c): c is Tag => isTag(c, "InlineCode")).map((c) => String(c.attributes.content));
        return;
      }
      headerBody.push(...rendered);
    });

    const sections: RenderableTreeNode[] = [];
    let current: { title: string; body: RenderableTreeNode[] } | null = null;
    const flush = () => {
      if (current) sections.push(new Tag("Section", { title: current.title, id: slug(current.title) }, classify(current.body)));
    };
    for (const child of sectionNodes) {
      if (child.type === "heading" && child.attributes.level === 2) {
        flush();
        current = { title: textOf(transformOne(child, config)), body: [] };
      } else {
        current!.body.push(...transformOne(child, config));
      }
    }
    flush();

    return new Tag("Page", {}, [
      new Tag("Header", { title, badges, declaration }, classify(headerBody)),
      ...sections,
    ]);
  },
};

export const config: Config = {
  nodes: {
    document,
    paragraph: { ...nodes.paragraph, render: "Paragraph" },
    heading: {
      ...nodes.heading,
      transform: (node, config) => {
        const children = node.transformChildren(config);
        return new Tag("Heading", { level: node.attributes.level, id: slug(textOf(children)) }, children);
      },
    },
    fence: {
      ...nodes.fence,
      transform: (node) =>
        new Tag("CodeBlock", {
          content: String(node.attributes.content).replace(/\n$/, ""),
          language: node.attributes.language,
        }),
    },
    code: { ...nodes.code, transform: (node) => new Tag("InlineCode", { content: node.attributes.content }) },
    link: { ...nodes.link, render: "DocLink" },
    list: {
      ...nodes.list,
      transform: (node, config) => new Tag("List", { ordered: node.attributes.ordered }, node.transformChildren(config)),
    },
    item: { ...nodes.item, render: "Item" },
    strong: { ...nodes.strong, render: "Strong" },
    em: { ...nodes.em, render: "Em" },
    blockquote: { ...nodes.blockquote, render: "Blockquote" },
  },
  tags: { member, readme },
};
