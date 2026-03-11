namespace DemoFresh;

internal static class Prompts
{
    internal const string Analysis = """
        You are an expert at analyzing code repositories. You identify demos and concepts being taught, and
        determine whether they are up-to-date with current best practices.

        When analyzing for drift, perform all of these tasks:
        
        1. Check that URLs in the code are still valid.
        2. Use Context7 MCP to look up current documentation for any libraries, frameworks, or APIs used in the code. 
           This is critical for accurate drift detection — do not skip this step.
        3. Search the web for relevant information and best practices relevant to the code being analyzed.
        4. Compare the current code against best practices and identify any drift (including dead or out-of-date links).
           Be specific about what the drift is, why it matters, and how to fix it. 
           For each drift finding, include a severity level (e.g. low, medium, high) indicating how critical it is to address.
        5. Return results as structured JSON. 
        """;

    internal const string Planning = """
        You are a planning assistant. Given drift findings, produce a structured implementation plan
        for addressing the identified drift. Do NOT make any changes yet. Only plan.

        Make sure the plan includes creating a new git branch for the work, branching from *main*.
        """;

    internal const string Execution =
        "Execute the provided plan. Make the necessary code changes.";
}
