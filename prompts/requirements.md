# DemoFresh Requirements

## Overview

DemoFresh is a command line tool that uses the [Copilot SDK](https://github.com/github/copilot-sdk) to automatically keep demo code and learning documentation up to date with current best practices. It does this by cloning a specified GitHub repo, identifying the concepts being demonstrated, searching the web for current best practices related to those concepts (and checking links mentioned in the repo itself), and then opening pull requests to address any drift between the existing demos and current best practices.

## Key Features

- Clone a specified GitHub repo and crawl its files to identify the concepts being demonstrated or taught.
- Search the web (using both URLs from the repo and web search capabilities) to find current best practices and usage for the demonstrated features/concepts.
- Follow the following process for each demo:
    - Identify any deviations between the existing demos and current best practices or features. This could include dependency version changes, changed APIs, deprecated features, differences in best practice, or new features that should be added to the demo.
    - Plan out updates needed (if any) to address the drift using the Copilot SDK's planning capabilities.
    - Depending on configuration, either create PRs for each plan or delegate each plan to the GitHub Copilot coding agent to be remediated in the cloud.
    - If no updates are needed, no action should be taken on the repo (except for reporting, see next step)!
- Use an email tool to email a report of the findings and actions taken to a configured owner.
    - The email tool should be coded in the DemoFresh solution and made available to Copilot as a custom tool.
- Thorough unit and integration tests to ensure the tool is working correctly and to prevent regressions.

## Tech Stack

- .NET 10 / C#
- .NET generic host including appsettings.json configuration for settings like email configuration, action configuration (create PR vs delegate to GitHub coding agent), etc.
- [Copilot SDK](https://github.com/github/copilot-sdk)
- Copilot SDK tool for allowing callbacks from the LLM to the DemoFresh code as shown [here](https://github.com/github/copilot-sdk/tree/main/dotnet#tools)
- SMTP email via MailKit (Gmail SMTP with app password) for email capabilities
