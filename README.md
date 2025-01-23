# rust-reflect-cli

A concise CLI tool for decompiling and analyzing Rust .NET assemblies.

## Why Use It?
- Quickly find hooks (e.g., `OnPlayerConnected`) and methods (e.g., `GetHeldEntity`) in Rust's actual codebase.
- Verify method signatures to avoid errors and inconsistencies during plugin development.
- Ensure compatibility with the latest game updates.

## Installation
```bash
dotnet tool install --global rust-reflect-cli
```
Or build from source:
```bash
git clone https://github.com/publicrust/rust-reflect-cli.git
cd rust-reflect-cli
dotnet build
```

## Key Commands
```bash
# Search for hooks, methods, or strings
rust-reflect search --input /path/to/Managed --string "OnPlayerConnected"

# Decompile an assembly or a specific type
rust-reflect decompile-type --input Assembly.dll --type "BasePlayer"

# Analyze method usage
rust-reflect analyze-method --input /path/to/Managed \
  --type "BasePlayer" --method "GetHeldEntity"
```

## Usage in Rust Plugin Development
1. **Hook Verification**: Before implementing code, confirm the existence of necessary hooks (e.g., `OnPlayerConnected`).
2. **Method Details**: For methods like `GetHeldEntity`, check where and how they are called in the actual assembly.
3. **Stay Updated**: After Rust updates, run `search` or `analyze` to identify changes in the game's codebase.

## License
Licensed under the MIT License. See the LICENSE file for details.

```

Use `rust-reflect-cli` as an essential tool to stay aligned with Rust's actual codebase and avoid potential issues in your plugins.
