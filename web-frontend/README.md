# Peglin Save Explorer Web Frontend

This is the React-based web frontend for the Peglin Save Explorer CLI tool.

## Setup

### Prerequisites

- Node.js 16+
- npm or yarn

### Installation

1. Navigate to the web-frontend directory:

```bash
cd web-frontend
```

2. Install dependencies:

```bash
npm install
```

### Development

To run the development server:

```bash
npm run dev
```

This will start the Vite development server on http://localhost:3000 with hot module replacement.

### Building for Production

To build the frontend for production:

```bash
npm run build
```

The built files will be output to the `dist/` directory and automatically copied to the C# project's output during release builds.

## Architecture

The web frontend is built with:

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Material-UI (MUI)** - Component library and design system
- **Vite** - Build tool and development server
- **React Router** - Client-side routing
- **MUI X Charts** - Data visualization
- **MUI X Data Grid** - Advanced data tables

### File Structure

```
src/
├── components/          # React components
│   ├── Dashboard.tsx    # Main dashboard with overview
│   ├── RunList.tsx      # Filterable run history table
│   ├── RunDetail.tsx    # Individual run details
│   ├── Statistics.tsx   # Charts and analytics
│   ├── FileUpload.tsx   # Save file upload interface
│   └── Navigation.tsx   # Navigation tabs
├── types.ts            # TypeScript interfaces
├── api.ts              # API client for C# backend
├── App.tsx             # Main app component
└── main.tsx            # Entry point
```

## Features

### Dashboard

- Overview cards showing total runs, wins, win rate, and best character class
- Charts showing wins by character class and average damage
- Recent runs list with quick details

### Run History

- Searchable and filterable data grid of all runs
- Filters for character class, win/loss, date range, damage range
- Sortable columns with proper data formatting
- Pagination for large datasets

### Statistics

- Detailed charts and analytics:
  - Win rate by character class
  - Most used orbs
  - Orb win rates
  - Activity over time
- Summary cards for each character class

### File Upload

- Drag-and-drop file upload interface
- Support for .data and .save files
- Instructions for finding save files on different platforms

## Integration with C# Backend

The frontend communicates with the C# CLI tool via REST API endpoints when the `web` command is used:

- `GET /api/runs` - Get all run history data
- `GET /api/runs/filtered` - Get filtered runs with query parameters
- `GET /api/runs/{id}` - Get individual run details
- `POST /api/load` - Upload and load a new save file
- `GET /api/export` - Export data as JSON or CSV

## Customization

### Theming

The app uses MUI's dark theme by default. You can customize the theme in `src/main.tsx`.

### Adding New Charts

New chart components can be added using MUI X Charts. See the Statistics component for examples.

### API Integration

The API client in `src/api.ts` can be extended to support additional endpoints as the C# backend evolves.
