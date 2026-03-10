Here are some thoughts on what this demo flow might look like. Let me know what you think.

- The demo app: "DemoFresh - The automated way to keep demos and learning docs perpetually fresh." 
    - We (mostly Copilot) would build this command line app that uses Copilot SDK to perform the following actions when run:
        - Clone a specified GitHub repo
        - Crawl files in the repo to identify the concepts they're trying to demonstrate or teach.
        - Search the web (using both URLs from the repo and its own web search capabilities) to find current best practices and usage for the demonstrated features/concepts.
        - For any deviations between the existing demos and current best practices, use "plan mode" (does the SDK even have plan mode? I think so but will double check) to plan updates to address the drift.
        - Depending on configuration, either create PRs for each plan or delegate each plan to the GitHub Copilot coding agent to be remediated in the cloud.
        - Use an email skill to email a report of its findings and actions to a configured owner.
    - The tool could be run on a timer either locally (as a cron job), in Azure Functions, or perhaps as a GitHub action.

- Demo flow:
    - [1.5 minutes] Start with a single slide highlighting the most important features and use-cases of Copilot CLI and SDK.
    - [1.0 minutes] Explain DemoFresh (a tool built with the Copilot CLI that uses the Copilot SDK) and show a report it generated as a demo.
    - [3.0 minutes] Show in DemoFresh's code how easy it is to use the SDK to create  programmatic Copilot coding experiences including :
        - Planning
        - Multi-turn operations
        - Skill usage
        - Integration with GitHub and/or the GitHub coding agent.
        - This could end up taking too long, so will need to carefully script out exactly what we want to show and discuss here.
    - Explain that the SDK is great for automated workflows but the Copilot CLI can help developers interactively build develop solutions (like DemoFresh) much faster than they could on their own.
    - Demonstrate Copilot CLI usage by adding a new feature to DemoFresh live.
        - [0.5 minutes] Show the Copilot-Instructions file we will have in place already with repo-wide guidance and knowledge.
        - [0.5 minutes] Show skills already available in the repo for improving Copilot SDK usage.
        - [2.0 minutes] Use the CLI's plan mode to plan a new feature.
            - Show useful features like prompt stacking, model selection, etc.
        - [4.0 minutes] Use autopilot mode to implement the feature and associated tests.
        - [1.0 minutes] Ask questions about the new feature and show how /ide allows working with VS Code using the Copilot CLI.
            - Also show how context can be selectively included with "@".
        - [1.0 minutes] Use /review to code review the change.
        - [0.5 minutes] Have Copilot CLI push and create a PR for the feature.
    - Ideas for the feature to add as part of the demo:
        - Create a "freshness score" that rates the repo (and maybe each demo?) on a scale from 0-100 for how fresh it is and includes that information in the report.
        - Add a --dry-run flag to DemoFresh that allows it to report on what it would change without actually making any changes.
        - Update DemoFresh's reporting to allow text messaging or Slack notifications instead of only email.
    