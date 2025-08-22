import "@mantine/core/styles.css";
import "./tailwind.css";
import React from "react";

import logoUrl from "../assets/logo.svg";
import {
  AppShell,
  Group,
  Image,
  MantineProvider,
  ScrollArea,
} from "@mantine/core";
import { NavLink } from "../components/NavLink";
import { CliNavLink } from "../components/CliNavLink";
import { useData } from "vike-react/useData";
import theme from "./theme.js";

export default function CliLayout({ children }: { children: React.ReactNode }) {
  // Get commands from the page data
  const data = useData<{
    commands?: Array<{ name: string; description: string }>;
    layoutProps?: { commands: Array<{ name: string; description: string }> };
  }>();

  const commands =
    data?.layoutProps?.commands ||
    data?.commands?.map((cmd) => ({
      name: cmd.name,
      description: cmd.description,
    })) ||
    [];
  return (
    <MantineProvider theme={theme}>
      <AppShell
        header={{ height: 60 }}
        navbar={{ width: 280, breakpoint: "md" }}
        padding="md"
      >
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

        <AppShell.Navbar p="md">
          <div style={{ marginBottom: "20px" }}>
            <h3
              style={{
                margin: "0 0 10px 0",
                fontSize: "16px",
                fontWeight: 600,
              }}
            >
              CLI Commands
            </h3>
            <CliNavLink href="/cli-commands" label="Overview" />
          </div>

          <ScrollArea style={{ height: "calc(100vh - 200px)" }}>
            <div
              style={{ display: "flex", flexDirection: "column", gap: "4px" }}
            >
              {Array.isArray(commands) && commands.map((command) => (
                <CliNavLink
                  key={String(command.name)}
                  href={`/cli-commands/${command.name}`}
                  label={String(command.name)}
                  description={String(command.description)}
                />
              ))}
            </div>
          </ScrollArea>
        </AppShell.Navbar>

        <AppShell.Main>{children}</AppShell.Main>
      </AppShell>
    </MantineProvider>
  );
}
