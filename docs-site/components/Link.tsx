import React from "react";

export function applyBaseUrl(href: string) {
  const baseUrl = import.meta.env.BASE_URL || "/";
  if (href.startsWith("/")) {
    return baseUrl.endsWith("/")
      ? `${baseUrl}${href.slice(1)}`
      : `${baseUrl}${href}`;
  }
  return href;
}

export function Link({
  href,
  children,
  className,
}: {
  href: string;
  children: React.ReactNode;
  className?: string;
}) {
  const finalHref = applyBaseUrl(href);
  return (
    <a href={finalHref} className={className}>
      {children}
    </a>
  );
}
