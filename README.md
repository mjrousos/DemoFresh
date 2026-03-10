# DemoFresh

**The automated way to keep demos and learning docs perpetually fresh.**

DemoFresh is a .NET 10 command-line tool that uses the [GitHub Copilot SDK](https://github.com/github/copilot-sdk) to automatically keep demo code and learning documentation up to date with current best practices. It clones a GitHub repo, uses AI to identify the demos and concepts being taught, searches the web for current best practices, and creates pull requests (or delegates to the GitHub Copilot coding agent) to fix any drift.

## How It Works

1. **Clone & scan** — Clones the target repo and enumerates source files.
2. **Identify demos** — Sends the file listing to a Copilot SDK session which identifies discrete demos/concepts.
3. **Analyze drift** — For each demo, Copilot checks for drift using web search and (optionally) Context7 MCP for up-to-date library documentation.
4. **Plan & remediate** — For demos with drift, Copilot generates an implementation plan and either:
   - Creates a PR with the fixes (`CreatePR` mode), or
   - Delegates to the GitHub Copilot coding agent for cloud-based remediation (`DelegateToCodingAgent` mode).
5. **Report** — Prints a console summary and (if email is configured) sends an HTML email report summarizing findings and actions taken.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [GitHub Copilot CLI](https://github.com/github/copilot-cli) installed and authenticated (`copilot` in PATH)
- [GitHub CLI](https://cli.github.com/) installed and authenticated (`gh auth login`)
- Git installed and in PATH
- Optional
    - A [Context7 API key](https://context7.com) for up-to-date library documentation
    - [Node.js 18+](https://nodejs.org/) (required for Context7 MCP server)
    - A Gmail account with a [Google Cloud OAuth2 client](https://console.cloud.google.com/apis/credentials) (Client ID and Client Secret) with the Gmail API enabled for email reports
        - For details on how to set up Google OAuth2 credentials for SMTP, see [this guide](https://github.com/jstedfast/MailKit/blob/master/GMailOAuth2.md).
        - Note that the first time the app tries to send email using the configured Gmail account, it will prompt you to complete the OAuth2 flow in a browser. After that, the access token is cached locally and refreshed automatically, so no further interaction is needed for email sending.

## Build

```bash
dotnet build
```

## Test

```bash
# Run all tests
dotnet test

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Configuration

Edit `src/DemoFresh/appsettings.json`:

```jsonc
{
  "DemoFresh": {
    "Repos": [
      {
        "Url": "https://github.com/owner/repo",  // GitHub repo URL
        "Branch": "main",                         // Branch to analyze (optional, defaults to "main")
        "Name": "My Demo Repo"                    // Display name (optional)
      }
    ],
    "ActionMode": "CreatePR",          // "CreatePR" or "DelegateToCodingAgent"
    "Model": "claude-sonnet-4.6",     // AI model (claude-sonnet-4.6, gpt-5, etc.)
    "ReportRecipient": "you@example.com",
    "Email": {
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": 587,
      "UseSsl": true,
      "SenderAddress": "your-email@gmail.com",
      "SenderName": "DemoFresh",
      "ClientId": "",                  // Google OAuth2 Client ID (use user secrets)
      "ClientSecret": ""               // Google OAuth2 Client Secret (use user secrets)
    },
    "Context7": {
      "Enabled": true,                         // Enable Context7 MCP for documentation lookup
      "Command": "npx",                        // Command to launch Context7 (default: npx)
      "Args": ["-y", "@upstash/context7-mcp"], // Args for the Context7 MCP server
      "ApiKey": ""                             // Context7 API key (use user secrets)
    }
  }
}
```

> **Tip:** Use [.NET user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) to keep credentials out of source control:
> ```bash
> cd src/DemoFresh
> dotnet user-secrets init
> dotnet user-secrets set "DemoFresh:Email:ClientId" "your-google-client-id"
> dotnet user-secrets set "DemoFresh:Email:ClientSecret" "your-google-client-secret"
> ```
>
> For Context7:
> ```bash
> dotnet user-secrets set "DemoFresh:Context7:ApiKey" "your-context7-api-key"
> ```

**Testing email:** Use `--test-email` to send a test email directly (bypasses the full pipeline):

```bash
dotnet run --project src/DemoFresh -- --test-email
dotnet run --project src/DemoFresh -- --test-email --test-email-to user@example.com
```

On first run, a browser window will open for Google OAuth2 consent. The token is cached locally for subsequent runs.

## Usage

**Analyze repos from config:**

```bash
dotnet run --project src/DemoFresh
```

**Analyze a single repo (ad-hoc):**

```bash
dotnet run --project src/DemoFresh -- --repo https://github.com/owner/repo
```

The `--repo` flag overrides the configured repo list for a one-off run.

**Logs** are written to both the console and `logs/demofresh-YYYYMMDD.log` by default (configurable via the `Serilog` section in appsettings.json).

## Project Structure

```
src/DemoFresh/
├── Program.cs                     Entry point (.NET Generic Host)
├── appsettings.json               Configuration
├── Configuration/                 Strongly-typed config models
├── Services/                      Core business logic
│   ├── AnalysisOrchestrator.cs    Main workflow (BackgroundService)
│   ├── CopilotSessionManager.cs   Copilot SDK client/session lifecycle
│   ├── DriftAnalyzer.cs           Demo identification & drift analysis
│   ├── PlanExecutor.cs            Multi-turn plan generation & execution
│   ├── PrService.cs               Git branch/commit/PR creation
│   ├── DelegationService.cs       Coding agent delegation
│   ├── ReportGenerator.cs         HTML & console report generation
│   ├── RepoService.cs             Repo cloning & file enumeration
│   ├── ProcessRunner.cs           Thin wrapper for CLI commands
│   └── SmtpSender.cs              Thin wrapper for SMTP
├── Tools/
│   └── EmailToolFactory.cs        Copilot SDK custom tool for sending email
└── Models/                        Data models (Demo, DriftFinding, etc.)

tests/DemoFresh.Tests/             xUnit tests (61 tests)
```

## License

MIT
