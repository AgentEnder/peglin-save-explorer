import type { Config } from "vike/types";
import vikeReact from "vike-react/config";
import Layout from "../layouts/LayoutDefault.js";

// Default config (can be overridden by pages)
// https://vike.dev/config

export default {
  // https://vike.dev/Layout
  Layout,

  // https://vike.dev/head-tags
  title: "Peglin Save Explorer",
  description:
    "Documentation for Peglin Save Explorer - A comprehensive tool for analyzing Peglin save files",
  prerender: true,

  extends: vikeReact,
} satisfies Config;
