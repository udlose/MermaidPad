# Contributing to MermaidPad

Thank you for your interest in contributing to MermaidPad! This guide will help you get started with development.

## Development Setup

### Prerequisites

- **.NET 9 SDK** - Required for building the C# application
- **Node.js 18+** - Required for TypeScript compilation and frontend tooling
- **npm 11.6.0+** - Package manager (included with Node.js)

### Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/udlose/MermaidPad.git
   cd MermaidPad
   ```

2. **Install npm dependencies**
   ```bash
   npm install
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

The build process will automatically:
- Compile TypeScript source files from `Assets/src/` to `Assets/dist/bundle.js`
- Generate SHA-256 hashes for asset integrity verification
- Embed all assets as resources in the .NET assembly

## TypeScript Development

MermaidPad uses TypeScript for frontend code to provide type safety and better maintainability.

### Project Structure

```
MermaidPad/
├── Assets/
│   ├── src/              # TypeScript source files (tracked in git)
│   │   ├── main.ts       # Main entry point and initialization
│   │   ├── theme.ts      # Theme management (light/dark)
│   │   ├── loading.ts    # Loading indicator management
│   │   ├── renderer.ts   # Mermaid diagram rendering
│   │   ├── export.ts     # PNG export functionality
│   │   ├── hover.ts      # Hover enhancement for diagrams
│   │   └── types.ts      # TypeScript type definitions
│   ├── dist/             # Compiled JavaScript (NOT tracked in git)
│   │   └── bundle.js     # Generated bundle (embedded as resource)
│   ├── index.html        # Main HTML template
│   ├── mermaid.min.js    # Mermaid library (external)
│   └── js-yaml.min.js    # YAML parser (external)
├── Services/             # .NET service layer
├── ViewModels/           # MVVM ViewModels
└── Views/                # Avalonia XAML views
```

### TypeScript Build Commands

```bash
# Production build (minified, no sourcemaps)
npm run build

# Development build (with sourcemaps)
npm run build:dev

# Watch mode (auto-rebuild on file changes)
npm run watch
```

### Linting

```bash
# Run ESLint on all TypeScript and HTML files
npm run lint

# Auto-fix linting issues
npm run lint:fix

# Lint for CI (fails on warnings)
npm run lint:ci
```

## Build System Architecture

### How It Works

1. **TypeScript Compilation** (`CompileTypeScript` target)
   - Runs `npm run build` before MSBuild's `GenerateAssetHashes` target
   - Uses esbuild to bundle `Assets/src/main.ts` → `Assets/dist/bundle.js`
   - Output is minified and tree-shaken for optimal size

2. **Hash Generation** (`GenerateAssetHashes` target)
   - Calculates SHA-256 hashes for all embedded assets including `bundle.js`
   - Generates `Generated/AssetHashes.cs` with hash dictionary
   - Hashes are used for runtime integrity verification

3. **Asset Embedding**
   - All files in `Assets/` (including `dist/bundle.js`) are embedded as resources
   - At runtime, assets are extracted to `%AppData%/MermaidPad/Assets/`
   - Integrity is verified by comparing file hashes against build-time hashes

4. **Integrity Verification**
   - `AssetHelper.cs` validates extracted assets match expected hashes
   - If tampered, assets are re-extracted from embedded resources
   - Prevents execution of modified/malicious code

## Making Changes to Frontend Code

### Editing TypeScript

1. Make changes to files in `Assets/src/`
2. Run `npm run watch` for live recompilation
3. Build the .NET project to test changes: `dotnet build`
4. Run the application to see your changes

### Adding New TypeScript Modules

1. Create new `.ts` file in `Assets/src/`
2. Import it in relevant modules (usually `main.ts`)
3. Types are automatically included via `types.ts`
4. No need to update build configuration (esbuild handles dependencies)

### Modifying HTML/CSS

- Edit `Assets/index.html` for HTML and CSS changes
- Keep styles in the `<style>` tag for inline CSS
- The HTML file is embedded and extracted at runtime

## Testing

### Running the Application

```bash
# Debug build
dotnet run

# Release build
dotnet run --configuration Release
```

### Testing Asset Integrity

The build system automatically verifies:
- All assets have valid SHA-256 hashes
- TypeScript compiles without errors
- Generated files match expected structure

## Pull Request Guidelines

1. **Create a feature branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**
   - Follow existing code style and conventions
   - Add comments for complex logic
   - Update tests if applicable

3. **Test your changes**
   ```bash
   npm run lint          # Check for linting errors
   dotnet build          # Ensure project builds
   dotnet run            # Test the application
   ```

4. **Commit your changes**
   ```bash
   git add .
   git commit -m "Brief description of changes"
   ```

5. **Push and create PR**
   ```bash
   git push origin feature/your-feature-name
   ```

## Code Style

### TypeScript

- Use **strict TypeScript** mode (enforced by `tsconfig.json`)
- Prefer `const` over `let`
- Use arrow functions for callbacks
- Add JSDoc comments for public functions
- Follow existing patterns in codebase

### C#

- Follow Microsoft C# coding conventions
- Use nullable reference types
- Add XML documentation for public APIs
- Follow existing patterns in codebase

## Common Development Tasks

### Updating Mermaid Version

1. Download new `mermaid.min.js` from [Mermaid releases](https://github.com/mermaid-js/mermaid/releases)
2. Replace `Assets/mermaid.min.js`
3. Test thoroughly with various diagram types
4. Build will automatically update hash

### Adding New Features

1. Identify where the feature belongs (frontend/backend)
2. For frontend: Add TypeScript modules in `Assets/src/`
3. For backend: Add C# classes in appropriate namespace
4. Update interfaces if adding C#/JavaScript interop
5. Test integration between frontend and backend

### Debugging

**Frontend (JavaScript/TypeScript):**
- Use browser DevTools in the embedded WebView
- Check console for errors
- Use `console.log()` for debugging (removed in production build)

**Backend (C#):**
- Use Visual Studio debugger
- Set breakpoints in C# code
- Check `SimpleLogger` output for diagnostic messages

## Questions?

If you have questions or need help:
- Open an issue on GitHub
- Check existing documentation in the repository
- Review code comments and examples

Thank you for contributing to MermaidPad!
