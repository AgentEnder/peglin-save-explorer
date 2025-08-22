import { NavLink as MantineNavLink } from "@mantine/core";
import { usePageContext } from "vike-react/usePageContext";

export function CliNavLink({ 
  href, 
  label, 
  description 
}: { 
  href: string; 
  label: string; 
  description?: string;
}) {
  const pageContext = usePageContext();
  const { urlPathname } = pageContext;
  const isActive = href === "/cli-commands" 
    ? urlPathname === href 
    : urlPathname === href || urlPathname.startsWith(href + '/');
  
  return (
    <MantineNavLink 
      href={href} 
      label={String(label)}
      description={description ? String(description) : undefined}
      active={isActive}
      style={{
        borderRadius: '4px',
        padding: '8px 12px'
      }}
    />
  );
}