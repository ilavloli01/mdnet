import { createHighlighterCoreSync, type ThemeRegistration } from "shiki/core";
import { createJavaScriptRegexEngine } from "shiki/engine/javascript";
import csharp from "@shikijs/langs/csharp";
import type { ReactNode } from "react";
import { usePage, pageHref } from "./context";

// Sentinel colors: each maps to Tailwind classes below, so the palette lives in one place.
const C = {
  plain: "#52525b",
  keyword: "#db2777",
  type: "#0d9488",
  method: "#2563eb",
  name: "#18181b",
  comment: "#a1a1aa",
  string: "#047857",
  number: "#c2410c",
} as const;

const classes: Record<string, string> = {
  [C.keyword]: "text-pink-600 font-medium",
  [C.type]: "text-teal-600 font-medium",
  [C.method]: "text-blue-600 font-semibold",
  [C.name]: "text-zinc-900",
  [C.comment]: "text-zinc-400",
  [C.string]: "text-emerald-700",
  [C.number]: "text-orange-700",
};

const theme: ThemeRegistration = {
  name: "mdnet-light",
  type: "light",
  colors: { "editor.foreground": C.plain, "editor.background": "#fcfcfc" },
  fg: C.plain,
  bg: "#fcfcfc",
  settings: [
    { settings: { foreground: C.plain } },
    { scope: ["comment", "punctuation.definition.comment"], settings: { foreground: C.comment } },
    {
      scope: ["keyword", "storage.modifier", "storage.type", "constant.language", "variable.language"],
      settings: { foreground: C.keyword },
    },
    {
      scope: [
        "keyword.type",
        "support.type",
        "support.class",
        "entity.name.type",
        "entity.other.inherited-class",
        "storage.type.cs",
      ],
      settings: { foreground: C.type },
    },
    { scope: ["entity.name.function", "support.function"], settings: { foreground: C.method } },
    {
      scope: ["variable.parameter", "entity.name.variable", "variable.other", "variable.object.property"],
      settings: { foreground: C.name },
    },
    { scope: ["string", "constant.character", "punctuation.definition.string"], settings: { foreground: C.string } },
    { scope: ["constant.numeric"], settings: { foreground: C.number } },
    { scope: ["keyword.operator", "punctuation", "meta.brace"], settings: { foreground: C.plain } },
  ],
};

const highlighter = createHighlighterCoreSync({
  themes: [theme],
  langs: [csharp],
  engine: createJavaScriptRegexEngine({ forgiving: true }),
});

const languages = new Set(["csharp", "cs", "c#"]);

/** How a C# fragment must be wrapped so the grammar sees it in a valid context. */
export type Wrap = "none" | "class" | "enum";

/**
 * Highlights code as React spans. Known documented types become links; everything else keeps the theme
 * color. Unknown languages render as plain text.
 */
export function Code({ code, language, wrap = "none" }: { code: string; language?: string; wrap?: Wrap }) {
  const page = usePage();
  const lang = (language ?? "").toLowerCase();
  if (!languages.has(lang)) {
    return <>{code}</>;
  }

  const prefix = wrap === "class" ? "class __ {\n" : wrap === "enum" ? "enum __ {\n" : "";
  const suffix = wrap === "none" ? "" : "\n}";
  const { tokens } = highlighter.codeToTokens(prefix + code + suffix, { lang: "csharp", theme: "mdnet-light" });
  const lines = wrap === "none" ? tokens : tokens.slice(1, -1);

  const out: ReactNode[] = [];
  lines.forEach((line, lineIndex) => {
    if (lineIndex > 0) out.push("\n");
    line.forEach((token, tokenIndex) => {
      const key = `${lineIndex}-${tokenIndex}`;
      const text = token.content;
      const color = token.color?.toLowerCase() ?? C.plain;
      const target = color !== C.method && /^[A-Za-z_]\w*$/.test(text) ? (page.pkg?.symbols.get(text) ?? page.site.symbols.get(text)) : undefined;
      if (target && target !== page.path) {
        out.push(
          <a key={key} href={pageHref(page.path, target)} className="text-teal-600 font-medium hover:underline decoration-teal-600/30 underline-offset-2">
            {text}
          </a>,
        );
        return;
      }

      const className = target ? classes[C.type] : classes[color];
      out.push(className ? <span key={key} className={className}>{text}</span> : text);
    });
  });

  return <>{out}</>;
}
