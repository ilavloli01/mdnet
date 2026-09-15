import { Children, isValidElement, type ReactNode } from "react";
import { Code, type Wrap } from "./highlight";
import { rewriteHref } from "./context";

type WithChildren<T = object> = T & { children?: ReactNode };

const codeBox = "bg-[#fcfcfc] rounded-xl ring-1 ring-zinc-200/60 shadow-sm overflow-x-auto";
const codeText = "block font-mono text-[13px] sm:text-sm leading-loose whitespace-pre text-zinc-600";

/** Enum values (`Added = 1`) need an enum body for the grammar; other members a class body. */
function wrapFor(signature: string): Wrap {
  return /^(\[\w+(\(.*\))?\]\n)*@?\w+( = [^;]+)?$/.test(signature.trim()) ? "enum" : "class";
}

export function Page({ children }: WithChildren) {
  return <>{children}</>;
}

export function Header({ title, badges, declaration, children }: WithChildren<{ title: string; badges: string[]; declaration?: string }>) {
  const genericStart = title.indexOf("<");
  const name = genericStart > 0 ? title.slice(0, genericStart) : title;
  const generics = genericStart > 0 ? title.slice(genericStart) : "";
  const [kind, ...modifiers] = badges;
  const body = Children.toArray(children);
  const hasBody = body.length > 0;

  return (
    <>
      <header className="mb-14">
        <div className="flex flex-wrap items-center gap-3 mb-5">
          {kind && (
            <span className="px-2 py-0.5 rounded bg-blue-50 text-blue-700 text-[11px] font-mono font-medium ring-1 ring-blue-700/10 uppercase tracking-widest">
              {kind}
            </span>
          )}
          <h1 className="text-3xl md:text-4xl font-heading font-semibold text-zinc-900 tracking-tight break-words">
            {name}
            {generics && <span className="text-zinc-400 font-mono font-medium text-2xl md:text-3xl">{generics}</span>}
          </h1>
          {modifiers.length > 0 && (
            <span className="flex gap-1.5">
              {modifiers.map((m) => (
                <span key={m} className="px-1.5 py-0.5 rounded bg-zinc-100 text-zinc-500 text-[11px] font-mono">
                  {m}
                </span>
              ))}
            </span>
          )}
        </div>
        {declaration && (
          <div className={`${codeBox} p-4 sm:p-5 mb-8`}>
            <code className={codeText}>
              <Code code={declaration} language="csharp" />
            </code>
          </div>
        )}
        {hasBody && <div className="space-y-5 max-w-2xl [&>p:first-child]:text-[16px] [&>p:first-child]:text-zinc-700">{body}</div>}
      </header>
      <div className="h-px w-full bg-zinc-100 mb-14" />
    </>
  );
}

export function Section({ title, id, children }: WithChildren<{ title: string; id: string }>) {
  return (
    <section className="mb-16 last:mb-0 scroll-mt-24" id={id}>
      <h2 className="text-xl font-heading font-semibold text-zinc-900 mb-8 tracking-tight">
        <a href={`#${id}`} className="hover:text-zinc-600">
          {title}
        </a>
      </h2>
      <div className="space-y-5">{children}</div>
    </section>
  );
}

export function Member({ id, signature, children }: WithChildren<{ id?: string; signature?: string }>) {
  return (
    <div className="group mb-14 last:mb-0 scroll-mt-24 relative" id={id}>
      {signature && (
        <div className={`${codeBox} p-4 sm:p-5 mb-5 relative`}>
          {id && (
            <a
              href={`#${id}`}
              aria-label="Link to this member"
              className="absolute right-3 top-3 text-zinc-300 opacity-0 group-hover:opacity-100 hover:text-zinc-500 font-mono text-xs"
            >
              #
            </a>
          )}
          <code className={codeText}>
            <Code code={signature} language="csharp" wrap={wrapFor(signature)} />
          </code>
        </div>
      )}
      <div className="space-y-4 px-1">{children}</div>
    </div>
  );
}

export function Params({ children }: WithChildren) {
  return <div className="space-y-2.5 pt-2">{children}</div>;
}

export function Param({ name, children }: WithChildren<{ name: string }>) {
  return (
    <p className="text-zinc-600 text-[15px] leading-relaxed flex items-start">
      <span className="font-mono text-[13px] font-medium text-zinc-900 bg-zinc-100 px-1.5 py-0.5 rounded mt-0.5 shrink-0">{name}</span>
      <span className="text-zinc-300 mx-2 mt-0.5 shrink-0">—</span>
      <span className="min-w-0">{children}</span>
    </p>
  );
}

const rowStyles: Record<string, string> = {
  returns: "text-blue-700 bg-blue-50 ring-blue-700/10",
  value: "text-emerald-700 bg-emerald-50 ring-emerald-700/10",
  throws: "text-rose-700 bg-rose-50 ring-rose-700/10",
  "see also": "text-zinc-600 bg-zinc-50 ring-zinc-700/10",
};

export function Row({ label, children }: WithChildren<{ label: string }>) {
  return (
    <div className="pt-2">
      <p className="text-zinc-600 text-[15px] leading-relaxed flex items-start">
        <span
          className={`font-mono text-[13px] font-medium px-1.5 py-0.5 rounded ring-1 mt-0.5 shrink-0 ${rowStyles[label] ?? rowStyles["see also"]}`}
        >
          {label}
        </span>
        <span className="text-zinc-300 mx-2 mt-0.5 shrink-0">—</span>
        <span className="min-w-0">{children}</span>
      </p>
    </div>
  );
}

export function RowTarget({ children }: WithChildren) {
  return <span className="mr-1.5">{children}</span>;
}

export function Remarks({ children }: WithChildren) {
  return (
    <div className="pt-3">
      <div className="text-zinc-500 text-[14px] leading-relaxed italic border-l-2 border-zinc-200 pl-4 py-1 space-y-3">{children}</div>
    </div>
  );
}

export function Paragraph({ tone, children }: WithChildren<{ tone?: "muted" }>) {
  return <p className={tone === "muted" ? "" : "text-zinc-800 text-[15px] leading-relaxed"}>{children}</p>;
}

export function Heading({ level, children }: WithChildren<{ level: number }>) {
  const Tag = `h${Math.min(Math.max(level, 1), 6)}` as "h3";
  return <Tag className="font-heading font-semibold text-zinc-900 tracking-tight mt-8 mb-3">{children}</Tag>;
}

export function CodeBlock({ content, language }: { content: string; language?: string }) {
  return (
    <div className={`${codeBox} p-5`}>
      <code className={codeText}>
        <Code code={content} language={language} />
      </code>
    </div>
  );
}

export function InlineCode({ content }: { content: string }) {
  return <code className="text-[13px] bg-zinc-100 px-1.5 py-0.5 rounded font-mono text-zinc-800">{content}</code>;
}

export function DocLink({ href, title, variant, children }: WithChildren<{ href: string; title?: string; variant?: "type" }>) {
  const codeOnly = Children.toArray(children).every((c) => isValidElement(c) && c.type === InlineCode);
  const className =
    variant === "type"
      ? "font-mono text-[13.5px] font-medium text-teal-700 hover:text-teal-900 [overflow-wrap:anywhere]"
      : codeOnly
        ? "[&>code]:text-teal-700 [&>code]:hover:bg-teal-50 [&>code]:transition-colors"
        : "text-blue-600 hover:text-blue-700 underline decoration-blue-600/30 underline-offset-2";
  return (
    <a href={rewriteHref(href)} title={title} className={className}>
      {children}
    </a>
  );
}

export function TypeList({ children }: WithChildren) {
  return <ul className="divide-y divide-zinc-100 border-y border-zinc-100">{children}</ul>;
}

export function TypeListItem({ children }: WithChildren) {
  return (
    <li className="grid gap-1 py-3 sm:grid-cols-[minmax(0,19rem)_1fr] sm:gap-6 text-[14px] leading-relaxed text-zinc-500">
      {children}
    </li>
  );
}

export function List({ ordered, children }: WithChildren<{ ordered?: boolean }>) {
  const className = "text-zinc-700 text-[15px] leading-relaxed space-y-1.5 pl-5 " + (ordered ? "list-decimal" : "list-disc marker:text-zinc-300");
  return ordered ? <ol className={className}>{children}</ol> : <ul className={className}>{children}</ul>;
}

export function Item({ children }: WithChildren) {
  return <li>{children}</li>;
}

export function Strong({ children }: WithChildren) {
  return <strong className="font-semibold text-zinc-900">{children}</strong>;
}

export function Em({ children }: WithChildren) {
  return <em>{children}</em>;
}

export function Blockquote({ children }: WithChildren) {
  return <blockquote className="text-zinc-600 text-[16px] leading-relaxed [&_p]:text-zinc-600 [&_p]:text-[16px]">{children}</blockquote>;
}

export const components = {
  Page,
  Header,
  Section,
  Member,
  Params,
  Param,
  Row,
  RowTarget,
  Remarks,
  Paragraph,
  Heading,
  CodeBlock,
  InlineCode,
  DocLink,
  TypeList,
  TypeListItem,
  List,
  Item,
  Strong,
  Em,
  Blockquote,
};

