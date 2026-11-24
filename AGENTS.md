# AppInventory

.NET 10 solution (classic `.sln`). Layered architecture: Clients -> Managers -> Engines/Accessors; Contracts are shared by all layers.

## Structure
- `src/Clients` - websites, APIs, background workers
- `src/Managers` - classes and logic that execute the work
- `src/Engines` - extra logic only when managers get heavy or code is shared by managers
- `src/Accessors` - databases, API calls, file access
- `src/Contracts` - contracts used throughout the project
- `src/Utilities` - cross-cutting logic (last resort)
- `tests` - unit test projects (one per layer, under `tests/<ProjectName>/`)

## Conventions
- New projects go on disk under the matching `src/` folder and are added with `dotnet sln add <proj> --solution-folder "<n>. <Layer>"`.
- Test projects go in `tests/<ProjectName>/` under the `9. Tests` solution folder.

## Further reading (load only when relevant)
- `Documentation/ApplicationInventory.DetailedRequirements.md` - full requirements; read when working on features or schema.
