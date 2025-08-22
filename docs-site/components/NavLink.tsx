import { Anchor } from "@mantine/core";
import { usePageContext } from "vike-react/usePageContext";
import { applyBaseUrl } from "./Link";

export function NavLink({ href, label }: { href: string; label: string }) {
  const pageContext = usePageContext();
  const { urlPathname } = pageContext;

  href = applyBaseUrl(href);
  const isActive =
    href === "/" ? urlPathname === href : urlPathname.startsWith(href);

  return (
    <Anchor
      href={href}
      style={{
        fontWeight: isActive ? 600 : 400,
        color: isActive ? "var(--mantine-primary-color-filled)" : "inherit",
        textDecoration: "none",
      }}
    >
      {label}
    </Anchor>
  );
}
