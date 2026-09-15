import type { ReactNode } from "react";
import { pageHref, slug, usePage, type NavNamespace } from "./context";

function BookIcon({ className }: { className: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" className={className} aria-hidden="true">
      <path d="M12 7v14" />
      <path d="M3 18a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h5a4 4 0 0 1 4 4 4 4 0 0 1 4-4h5a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1h-6a3 3 0 0 0-3 3 3 3 0 0 0-3-3z" />
    </svg>
  );
}

function ChevronIcon() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" className="w-3 h-3 text-zinc-300 shrink-0" aria-hidden="true">
      <path d="m9 18 6-6-6-6" />
    </svg>
  );
}

const filterScript = `document.querySelectorAll("[data-filter]").forEach(function(input){input.addEventListener("input",function(){var q=input.value.trim().toLowerCase();var nav=input.closest("nav");nav.querySelectorAll("[data-item]").forEach(function(li){li.hidden=q!==""&&li.getAttribute("data-item").indexOf(q)<0});nav.querySelectorAll("[data-group]").forEach(function(g){g.hidden=!g.querySelector("[data-item]:not([hidden])")})})});`;

function Navigation() {
  const page = usePage();
  const { site, pkg } = page;
  const rootHref = pageHref(page.path, "index.md");

  return (
    <nav className="text-[13px]">
      <a href={rootHref} className="flex items-center gap-2 font-heading font-semibold text-zinc-900 mb-6">
        <BookIcon className="w-4 h-4 text-zinc-500" />
        API documentation
      </a>

      {pkg ? (
        <>
          <a href={pageHref(page.path, `${pkg.id}/index.md`)} className="block mb-4">
            <span className="font-semibold text-zinc-900">{pkg.id}</span>
            {pkg.version && <span className="ml-2 font-mono text-[11px] text-zinc-400">{pkg.version}</span>}
          </a>
          <input
            data-filter=""
            type="search"
            placeholder="Filter types"
            className="w-full mb-6 px-2.5 py-1.5 rounded-md bg-white ring-1 ring-zinc-200 placeholder:text-zinc-400 text-zinc-800 focus:outline-none focus:ring-zinc-400"
          />
          {pkg.namespaces.map((ns) => (
            <NamespaceGroup key={ns.name} ns={ns} />
          ))}
        </>
      ) : (
        <ul className="space-y-1">
          {site.packages.map((p) => (
            <li key={p.id}>
              <a href={pageHref(page.path, `${p.id}/index.md`)} className="block py-1 text-zinc-600 hover:text-zinc-900">
                {p.id}
                {p.version && <span className="ml-2 font-mono text-[11px] text-zinc-400">{p.version}</span>}
              </a>
            </li>
          ))}
        </ul>
      )}
      <script dangerouslySetInnerHTML={{ __html: filterScript }} />
    </nav>
  );
}

function NamespaceGroup({ ns }: { ns: NavNamespace }) {
  const page = usePage();
  return (
    <div data-group="" className="mb-6">
      <div className="px-1 mb-1.5 text-[11px] font-mono font-medium uppercase tracking-wider text-zinc-400 break-all">{ns.name}</div>
      <ul>
        {ns.types.map((type) => {
          const current = type.path === page.path;
          return (
            <li key={type.path} data-item={type.name.toLowerCase()}>
              <a
                href={pageHref(page.path, type.path)}
                aria-current={current ? "page" : undefined}
                className={`block px-2 py-1 rounded-md font-mono text-[12.5px] truncate ${current ? "bg-zinc-100 text-zinc-900 font-medium" : "text-zinc-600 hover:text-zinc-900 hover:bg-zinc-100/60"}`}
              >
                {type.name}
              </a>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function Breadcrumb({ namespace, title }: { namespace?: string; title: string }) {
  const page = usePage();
  const crumbs: { label: string; href?: string }[] = [];
  if (page.pkg) {
    crumbs.push({ label: page.pkg.id, href: pageHref(page.path, `${page.pkg.id}/index.md`) });
    if (namespace) crumbs.push({ label: namespace });
  }
  const isIndex = page.path.endsWith("index.md");
  if (!isIndex) crumbs.push({ label: title.replace(/<.*$/, "") });

  return (
    <div className="flex items-center justify-between gap-4 mb-8">
      <div className="flex flex-wrap items-center text-[13px] text-zinc-500 gap-2 font-medium min-w-0">
        <BookIcon className="w-3.5 h-3.5 shrink-0" />
        {crumbs.length === 0 && <span className="text-zinc-900">API documentation</span>}
        {crumbs.map((crumb, i) => (
          <span key={i} className="flex items-center gap-2 min-w-0">
            {i > 0 && <ChevronIcon />}
            {crumb.href && i < crumbs.length - 1 ? (
              <a href={crumb.href} className="hover:text-zinc-900 truncate">
                {crumb.label}
              </a>
            ) : (
              <span className={`truncate ${i === crumbs.length - 1 ? "text-zinc-900" : ""}`}>{crumb.label}</span>
            )}
          </span>
        ))}
      </div>
      <a
        href={page.path.split("/").at(-1)}
        className="shrink-0 text-[12px] font-mono text-zinc-400 hover:text-zinc-700 px-2 py-0.5 rounded ring-1 ring-zinc-200 hover:ring-zinc-300"
        title="This page as Markdown"
      >
        .md
      </a>
    </div>
  );
}

export function Layout({ title, namespace, sections, children }: { title: string; namespace?: string; sections: string[]; children: ReactNode }) {
  const page = usePage();
  const siteName = page.pkg ? `${page.pkg.id} API` : "API documentation";

  return (
    <html lang="en">
      <head>
        <meta charSet="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <meta name="generator" content="mdnet" />
        <title>{title === siteName ? title : `${title} · ${siteName}`}</title>
        {page.pkg?.description && page.path.endsWith("index.md") && <meta name="description" content={page.pkg.description} />}
        <link rel="stylesheet" href={pageHref(page.path, "site.css")} />
        <link rel="alternate" type="text/markdown" href={page.path.split("/").at(-1)} />
      </head>
      <body className="bg-white text-zinc-900 font-sans antialiased">
        <div className="flex min-h-screen">
          <aside className="hidden lg:block w-72 shrink-0 border-r border-zinc-100 bg-zinc-50/50">
            <div className="sticky top-0 max-h-screen overflow-y-auto px-5 py-8">
              <Navigation />
            </div>
          </aside>

          <main className="flex-1 min-w-0 bg-white">
            <details className="lg:hidden border-b border-zinc-100 px-6 py-3">
              <summary className="cursor-pointer text-[13px] font-medium text-zinc-600">Browse</summary>
              <div className="pt-4">
                <Navigation />
              </div>
            </details>
            <div className="max-w-[800px] mx-auto px-6 py-12 md:px-12 lg:px-20">
              <Breadcrumb namespace={namespace} title={title} />
              {children}
            </div>
          </main>

          {sections.length > 1 && (
            <aside className="hidden xl:block w-52 shrink-0">
              <div className="sticky top-0 px-4 py-12 text-[13px]">
                <div className="mb-3 text-[11px] font-mono font-medium uppercase tracking-wider text-zinc-400">On this page</div>
                <ul className="space-y-1.5 border-l border-zinc-100">
                  {sections.map((section) => (
                    <li key={section}>
                      <a href={`#${slug(section)}`} className="block -ml-px pl-3 border-l border-transparent text-zinc-500 hover:text-zinc-900 hover:border-zinc-300">
                        {section}
                      </a>
                    </li>
                  ))}
                </ul>
              </div>
            </aside>
          )}
        </div>
      </body>
    </html>
  );
}
