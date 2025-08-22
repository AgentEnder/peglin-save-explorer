# Peglin Save Explorer Documentation Site

This is the documentation website for Peglin Save Explorer, built with Vike, React, Mantine, and Tailwind CSS.

## Development

Install dependencies:
```bash
npm install
```

Start the development server:
```bash
npm run dev
```

The site will be available at http://localhost:3000

## Building

Build the static site with prerendering:
```bash
npm run build
```

The built files will be in the `dist/client` directory, ready for deployment to any static hosting service.

## Structure

- `/pages` - Documentation pages
  - `/index` - Homepage with overview and quick start
  - `/getting-started` - Installation and setup guide
  - `/cli-commands` - Complete CLI command reference
  - `/web-frontend` - Web interface documentation
- `/layouts` - Page layouts and theme configuration
- `/components` - Reusable React components

## Features

- **Static Site Generation**: Prerendering enabled for fast loading and SEO
- **Responsive Design**: Built with Tailwind CSS for mobile-friendly layouts
- **Navigation Sidebar**: Easy navigation with Mantine components
- **Syntax Highlighting**: Code examples with proper formatting
- **Documentation Coverage**: Complete reference for all CLI commands and web interface features

## Technology Stack

- **Vike**: File-based routing and SSG framework
- **React**: UI components
- **Mantine**: Component library for navigation and layout
- **Tailwind CSS**: Utility-first CSS framework
- **TypeScript**: Type safety

## Deployment

After building, the `dist/client` directory contains all static files ready for deployment to:
- GitHub Pages
- Netlify
- Vercel
- Any static hosting service

## Adding New Pages

To add a new documentation page:

1. Create a new directory under `/pages`
2. Add a `+Page.tsx` file with your content
3. Update the navigation in `/layouts/LayoutDefault.tsx`
4. Build and test the site

