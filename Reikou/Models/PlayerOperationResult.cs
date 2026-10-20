using System;

namespace Reikou.Models;

public sealed record PlayerOperationResult(string Operation, bool Succeeded, string Message, DateTimeOffset Timestamp);
