# GitHub Actions for PushDeliveredQueue

This project includes several GitHub Actions workflows to automate testing and CI/CD processes.

## Available Workflows

### 1. CI (`ci.yml`)
**Purpose**: Fast, basic CI pipeline for quick feedback
- **Triggers**: Push to main/develop, Pull Requests
- **Runs**: Ubuntu latest
- **Features**: 
  - Builds the project
  - Runs all tests
  - Uploads test results as artifacts
- **Duration**: ~2-3 minutes

### 2. Unit Tests (`unit-tests.yml`)
**Purpose**: Dedicated workflow for unit tests with coverage
- **Triggers**: Push to main/develop, Pull Requests
- **Runs**: Ubuntu, Windows
- **Features**:
  - Cross-platform unit testing
  - Code coverage reporting
  - Test result artifacts
  - Coverage report generation
- **Duration**: ~3-5 minutes

### 3. Integration Tests (`test.yml`)
**Purpose**: Comprehensive integration testing across platforms
- **Triggers**: Push to main/develop, Pull Requests
- **Runs**: Ubuntu, Windows
- **Features**:
  - Cross-platform integration testing
  - Code coverage reporting
  - Test result artifacts
  - Coverage report generation
- **Duration**: ~4-6 minutes

### 4. Functional Tests (`functional-tests.yml`)
**Purpose**: Dedicated workflow for functional tests
- **Triggers**: Push to main/develop, Pull Requests, Manual dispatch
- **Runs**: Ubuntu latest
- **Features**:
  - Longer timeout (10 minutes)
  - Detailed logging
  - Manual trigger capability
  - Code coverage reporting
- **Duration**: ~5-8 minutes

## How to Use

### Automatic Execution
Workflows run automatically on:
- Every push to `main` or `develop` branches
- Every pull request targeting `main` or `develop`

### Manual Execution
1. Go to your GitHub repository
2. Click on the "Actions" tab
3. Select the workflow you want to run
4. Click "Run workflow"
5. Choose the branch and click "Run workflow"

### Viewing Results
1. **Test Results**: Download artifacts from the Actions tab
2. **Coverage Reports**: Available as HTML reports in artifacts
3. **Logs**: View detailed logs in the Actions tab

## Workflow Configuration

### Branches
Workflows are configured to run on:
- `main` branch
- `develop` branch

### .NET Version
All workflows use .NET 9.0.x

### Test Configuration
- **Configuration**: Release
- **Verbosity**: Normal
- **Parallelization**: Enabled (where applicable)

## Customization

### Adding New Workflows
1. Create a new `.yml` file in `.github/workflows/`
2. Follow the existing pattern
3. Configure triggers and steps as needed

### Modifying Existing Workflows
- Edit the corresponding `.yml` file
- Push changes to trigger the workflow
- Monitor results in the Actions tab

### Environment Variables
You can add environment variables in the workflow files:
```yaml
env:
  DOTNET_CLI_TELEMETRY_OPTOUT: 1
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: 1
```

## Troubleshooting

### Common Issues
1. **Build Failures**: Check for missing dependencies or compilation errors
2. **Test Failures**: Review test logs for specific failure reasons
3. **Timeout Issues**: Increase timeout values for long-running tests
4. **Platform-Specific Issues**: Check if tests fail on specific operating systems

### Debugging
1. Enable debug logging by adding `--verbosity detailed` to test commands
2. Check artifact uploads for detailed test results
3. Review workflow logs for step-by-step execution details

## Best Practices

1. **Keep Workflows Fast**: Use parallel jobs where possible
2. **Cache Dependencies**: Consider adding dependency caching for faster builds
3. **Fail Fast**: Configure workflows to fail early on critical errors
4. **Artifact Management**: Clean up old artifacts to save storage
5. **Security**: Use GitHub secrets for sensitive information

## Integration with IDE

### Visual Studio Code
- Install the "GitHub Actions" extension
- View workflow status directly in VS Code
- Trigger workflows from the command palette

### Visual Studio
- Use the GitHub integration features
- View Actions status in the Team Explorer
- Clone and manage workflows from the IDE

## Next Steps

Consider adding:
1. **Deployment workflows** for automatic releases
2. **Security scanning** with tools like CodeQL
3. **Performance testing** workflows
4. **Documentation generation** workflows
5. **Package publishing** workflows for NuGet packages
