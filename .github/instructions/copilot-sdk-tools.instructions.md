---
applyTo: "src/DemoFresh/Tools/**"
---
# Copilot SDK Tool Implementation Guide

When creating or modifying Copilot SDK tools in this directory:

1. Each tool must be created using `AIFunctionFactory.Create` from Microsoft.Extensions.AI
2. Tool parameters should use `[Description("...")]` attributes for clear LLM understanding
3. Tools should return JSON-serializable objects
4. Tools should handle their own errors gracefully and return error info rather than throwing
5. Tools should be registered with the CopilotSession via the `Tools` property in `SessionConfig`
6. Follow this pattern:

```csharp
var myTool = AIFunctionFactory.Create(
    async ([Description("Parameter description")] string param) => {
        // Implementation
        return new { Success = true, Message = "Done" };
    },
    "tool_name",
    "Description of what the tool does for the LLM");
```
