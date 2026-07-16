# TheMatrix

The standalone development repository for **Feeder MCP (Matrix AI Connector)**,
a Unity Editor package that bundles its local MCP server and can be installed independently
from FeederBase.

## Requirements

- Unity 2022.3 or newer
- Windows x64 for the currently bundled local server
- Git installed on the machine that opens the Unity project

## Install in another Unity project

Open **Window > Package Manager**, click **+**, choose **Add package from git URL**, and paste:

```text
https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp
```

Or add the dependency directly to the target project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.feeder.mcp": "https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp"
  }
}
```

No scoped registry or FeederBase package is required.

## Install a fixed release

Once a release tag exists, append it to the URL so builds do not change when `main` changes:

```text
https://github.com/BingoBoiz/TheMatrix.git?path=/Packages/com.feeder.mcp#v0.83.0
```

The Git tag and the `version` in `Packages/com.feeder.mcp/package.json` must match.

## Use

After Unity finishes compiling, open **Tools > Feeder > Matrix AI Connector**.

## Repository layout

- `Packages/com.feeder.mcp`: the distributable UPM package
- `Assets`, `ProjectSettings`, and the remaining `Packages` files: the local development project

Consumers only receive `Packages/com.feeder.mcp` when they install the Git URL above.

## Release checklist

1. Update `Packages/com.feeder.mcp/package.json`.
2. Add the release notes to `Packages/com.feeder.mcp/CHANGELOG.md`.
3. Open this project in the minimum supported Unity version and verify there are no compile errors.
4. Commit the package, create a matching tag such as `v0.83.0`, and push the commit and tag.

## License

MIT. See [LICENSE](LICENSE).
