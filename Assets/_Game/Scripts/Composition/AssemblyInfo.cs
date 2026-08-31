using System.Runtime.CompilerServices;

// Exposes internals to the EditMode test assembly (bootstrap approval logic).
[assembly: InternalsVisibleTo("Game.Tests.EditMode")]
