using System;
using System.Collections.Generic;
using Feeder.McpPlugin.Common.Hub.Client;
using R3;

namespace Feeder.McpPlugin;

public interface IPromptManager : IClientPromptHub, IDisposable
{
	Observable<Unit> OnPromptsUpdated { get; }

	int EnabledPromptsCount { get; }

	int TotalPromptsCount { get; }

	IEnumerable<IRunPrompt> GetAllPrompts();

	bool HasPrompt(string name);

	bool AddPrompt(IRunPrompt runner);

	bool RemovePrompt(string name);

	bool IsPromptEnabled(string name);

	bool SetPromptEnabled(string name, bool enabled);
}
