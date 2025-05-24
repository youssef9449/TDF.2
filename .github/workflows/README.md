# TODO Detection System

This directory contains GitHub Actions workflows for automated code quality checks, including TODO detection.

## TODO Detection Workflow

The `todo-check.yml` workflow automatically checks for TODO comments in the codebase during pull requests and pushes to the main branch.

### Features

- Detects various types of TODO markers (TODO, FIXME, XXX, HACK, etc.)
- Configurable severity levels for different types of markers
- Generates detailed reports of found TODOs
- Can be configured to fail builds if TODOs are found
- Excludes build artifacts and third-party code
- Supports multiple file types (.cs, .xaml, .md, etc.)

### Configuration

The TODO detection system can be configured using the `.todo-config.json` file in the root directory. Available options:

- `patterns`: Array of patterns to search for
- `extensions`: File extensions to check
- `excludePaths`: Paths to exclude from checking
- `failOnTodo`: Whether to fail the build if TODOs are found
- `maxTodos`: Maximum number of TODOs allowed (0 for unlimited)
- `reportFormat`: Format of the generated report
- `severityLevels`: Severity level for each pattern type

### Reports

The workflow generates a TODO report as a build artifact. You can find it in the GitHub Actions run summary under the "Artifacts" section.

### Usage

1. The workflow runs automatically on:
   - Pull requests to the main branch
   - Pushes to the main branch

2. To manually run the workflow:
   - Go to the "Actions" tab in GitHub
   - Select "TODO Detection" workflow
   - Click "Run workflow"

### Best Practices

1. Use appropriate TODO markers:
   - `TODO`: For planned improvements
   - `FIXME`: For bugs that need fixing
   - `XXX`: For critical issues
   - `HACK`: For temporary workarounds

2. Include context in TODO comments:
   ```csharp
   // TODO: Implement caching for better performance
   // FIXME: Handle null reference in edge case
   ```

3. Add ticket/issue numbers when applicable:
   ```csharp
   // TODO: #123 Implement user preferences
   ```

4. Set deadlines for TODOs:
   ```csharp
   // TODO: Remove deprecated API by 2024-12-31
   ```

### Customization

To customize the TODO detection:

1. Edit `.todo-config.json` to modify detection patterns and settings
2. Modify `todo-check.yml` to change workflow behavior
3. Add custom patterns or file types as needed

### Troubleshooting

If the workflow fails:

1. Check the GitHub Actions logs for details
2. Verify the configuration in `.todo-config.json`
3. Ensure the workflow has necessary permissions
4. Check for syntax errors in the configuration

### Contributing

To improve the TODO detection system:

1. Fork the repository
2. Make your changes
3. Submit a pull request
4. Ensure all tests pass
5. Update documentation as needed 