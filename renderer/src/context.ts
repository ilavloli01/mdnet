import { createContext, useContext } from "react";

export interface NavType {
  name: string;
  /** Page path relative to the docs root, e.g. Sample.Lib/Core.Data/DbContext.md */
  path: string;
}

export interface NavNamespace {
  name: string;
  types: NavType[];
}

export interface SitePackage {
  id: string;
  version?: string;
  description?: string;
  namespaces: NavNamespace[];
  /** Simple type name → page path within this package. */
  symbols: Map<string, string>;
}

export interface Site {
  packages: SitePackage[];
  /** Simple type name (no generics) → page path. First package wins on collisions. */
  symbols: Map<string, string>;
}

export interface PageInfo {
  /** Markdown page path relative to the docs root, forward slashes. */
  path: string;
  site: Site;
  pkg?: SitePackage;
}

export const PageContext = createContext<PageInfo | null>(null);

export function usePage(): PageInfo {
  const page = useContext(PageContext);
  if (!page) throw new Error("PageContext missing");
  return page;
}

/** Relative href from one page to another; .md targets become .html. */
export function pageHref(from: string, to: string): string {
  const [target, hash] = splitHash(to);
  const fromParts = from.split("/").slice(0, -1);
  const toParts = target.split("/");
  let common = 0;
  while (common < fromParts.length && common < toParts.length - 1 && fromParts[common] === toParts[common]) common++;
  const rel = [...Array(fromParts.length - common).fill(".."), ...toParts.slice(common)].join("/");
  return toHtml(rel) + (hash ? `#${hash}` : "");
}

/** Rewrites a link written in a Markdown page (relative to it) for the HTML site. */
export function rewriteHref(href: string): string {
  if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith("//") || href.startsWith("#")) return href;
  const [target, hash] = splitHash(href);
  return toHtml(target) + (hash ? `#${hash}` : "");
}

/** Resolves a link relative to a page into a docs-root path. */
export function resolvePath(from: string, href: string): string {
  const parts = from.split("/").slice(0, -1);
  for (const segment of splitHash(href)[0].split("/")) {
    if (segment === "..") parts.pop();
    else if (segment !== "." && segment !== "") parts.push(segment);
  }
  return parts.join("/");
}

function splitHash(href: string): [string, string | undefined] {
  const i = href.indexOf("#");
  return i < 0 ? [href, undefined] : [href.slice(0, i), href.slice(i + 1)];
}

function toHtml(path: string): string {
  return path.endsWith(".md") ? path.slice(0, -3) + ".html" : path;
}

export function slug(text: string): string {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-|-$/g, "");
}
