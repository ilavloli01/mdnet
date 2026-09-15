import Markdoc, { type Node } from "@markdoc/markdoc";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { resolvePath, type Site, type SitePackage } from "./context";

/** Plain text of a Markdoc AST node. */
export function astText(node: Node): string {
  if (node.type === "text" || node.type === "code") return String(node.attributes.content ?? "");
  return node.children.map(astText).join("");
}

/** Discovers package folders (those with mdnet.json) and builds navigation from their index.md. */
export function loadSite(root: string): Site {
  const packages: SitePackage[] = [];
  const symbols = new Map<string, string>();

  for (const entry of readdirSync(root, { withFileTypes: true }).sort((a, b) => a.name.localeCompare(b.name))) {
    if (!entry.isDirectory()) continue;
    const manifestPath = join(root, entry.name, "mdnet.json");
    if (!existsSync(manifestPath)) continue;

    const manifest = JSON.parse(readFileSync(manifestPath, "utf8")) as { id: string; version?: string };
    const pkg: SitePackage = {
      id: manifest.id,
      version: manifest.version,
      group: manifest.id.split(".")[0]!,
      meta: {},
      features: [],
      symbols: new Map(),
    };
    const indexPath = join(root, entry.name, "index.md");
    if (existsSync(indexPath)) {
      const indexPage = `${entry.name}/index.md`;
      const ast = Markdoc.parse(readFileSync(indexPath, "utf8"));
      pkg.meta = parseFrontmatter(String(ast.attributes.frontmatter ?? ""));
      pkg.version ??= pkg.meta.version;
      pkg.description = pkg.meta.description;
      for (const child of ast.children) {
        // "## Feature" sections hold the feature's own types, then "### Nested.Namespace" groups.
        if (child.type === "heading" && child.attributes.level === 2) {
          pkg.features.push({ title: astText(child), namespaces: [{ types: [] }] });
        } else if (child.type === "heading" && child.attributes.level === 3) {
          pkg.features.at(-1)?.namespaces.push({ name: astText(child), types: [] });
        } else if (child.type === "list") {
          const ns = pkg.features.at(-1)?.namespaces.at(-1);
          for (const item of child.children) {
            const link = [...item.walk()].find((n) => n.type === "link");
            if (!ns || !link) continue;
            const path = resolvePath(indexPage, String(link.attributes.href));
            const name = astText(link);
            ns.types.push({ name, path });
            const simple = name.replace(/<.*$/, "").split(".").at(-1)!;
            if (!pkg.symbols.has(simple)) pkg.symbols.set(simple, path);
            if (!symbols.has(simple)) symbols.set(simple, path);
          }
        }
      }
    }
    packages.push(pkg);
  }

  return { packages, symbols };
}

/** mdnet frontmatter: one `key: value` per line; values may be JSON-quoted. */
export function parseFrontmatter(source: string): Record<string, string> {
  const result: Record<string, string> = {};
  for (const line of source.split(/\r?\n/)) {
    const separator = line.indexOf(":");
    if (separator <= 0) continue;
    let value = line.slice(separator + 1).trim();
    if (value.startsWith('"')) {
      try {
        value = String(JSON.parse(value));
      } catch {
        continue;
      }
    }
    result[line.slice(0, separator).trim()] = value;
  }
  return result;
}

/** All Markdown pages below the docs root: the root index plus everything inside package folders. */
export function listPages(root: string): string[] {
  const pages: string[] = [];
  if (existsSync(join(root, "index.md"))) pages.push("index.md");
  const folders = readdirSync(root, { withFileTypes: true })
    .filter((e) => e.isDirectory() && existsSync(join(root, e.name, "mdnet.json")))
    .map((e) => e.name);
  for (const folder of folders) {
    const walk = (relative: string) => {
      for (const entry of readdirSync(join(root, relative), { withFileTypes: true })) {
        const child = `${relative}/${entry.name}`;
        if (entry.isDirectory()) walk(child);
        else if (entry.name.endsWith(".md")) pages.push(child);
      }
    };
    walk(folder);
  }
  return pages;
}
