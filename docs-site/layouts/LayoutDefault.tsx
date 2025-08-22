import "@mantine/core/styles.css";
import "./tailwind.css";

import logoUrl from "../assets/logo.svg";
import { AppShell, Group, Image, MantineProvider } from "@mantine/core";
import { NavLink } from "../components/NavLink";
import theme from "./theme.js";

export default function LayoutDefault({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <MantineProvider theme={theme}>
      <AppShell header={{ height: 60 }} padding="md">
        <AppShell.Header>
          <Group h="100%" px="md" justify="space-between" wrap="nowrap">
            <a
              href="/"
              style={{
                display: "flex",
                alignItems: "center",
                textDecoration: "none",
                color: "inherit",
                whiteSpace: "nowrap",
                overflow: "hidden",
              }}
            >
              <Image
                h={40}
                fit="contain"
                src={logoUrl}
                style={{
                  marginRight: "10px",
                  flexShrink: 0,
                  width: "min-content",
                }}
              />
              <span
                style={{
                  fontSize: "20px",
                  fontWeight: "bold",
                  whiteSpace: "nowrap",
                }}
              >
                Peglin Save Explorer
              </span>
            </a>

            <Group gap="md" visibleFrom="sm">
              <NavLink href="/" label="Home" />
              <NavLink href="/getting-started" label="Getting Started" />
              <NavLink href="/cli-commands" label="CLI Commands" />
              <NavLink href="/web-frontend" label="Web Frontend" />
            </Group>
          </Group>
        </AppShell.Header>
        <AppShell.Main>
          <div className="container mx-auto max-w-2xl px-4 py-8">
            {children}
          </div>
        </AppShell.Main>
      </AppShell>
    </MantineProvider>
  );
}
