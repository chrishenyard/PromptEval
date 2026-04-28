# AI.Evaluation

`AI.Evaluation` is a .NET 10 console-based project for experimenting with LLM chat workflows and AI quality evaluation scenarios.

## What this repository contains

- **`src/AI.Evaluation`**
  - Interactive console chat session using Semantic Kernel
  - Streaming assistant responses in the terminal
  - Token usage tracking and automatic chat history reset near context limits

- **`src/AI.Evaluation.Test`**
  - MSTest-based AI evaluation scenarios
  - Quality evaluators for agent behavior (for example, tool-call accuracy, task adherence, and intent resolution)
  - Reporting support for evaluation runs

## Requirements

- .NET 10 SDK
- Valid model/service configuration (for example OpenAI or Azure OpenAI)
- Environment/app settings configured for runtime and tests

## Running the console app

Run the main console project from `src/AI.Evaluation` after configuring settings/secrets.

## Running tests

Run MSTest from `src/AI.Evaluation.Test` in Visual Studio or with `dotnet test`.

> **Important:** MSTest scenarios in this repository require a model that supports **tool/function calling**.  
> If the configured model does not support tool calling, those tests will fail or produce invalid evaluation behavior.
