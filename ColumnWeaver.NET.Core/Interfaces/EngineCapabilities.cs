namespace ColumnWeaver.NET.Core.Interfaces
{
    /// <summary>
    /// Describes the compile-time and typing capabilities of an
    /// <see cref="IExpressionEngine"/> implementation.
    /// </summary>
    /// <remarks>
    /// Consumed by <c>ExecutionPlanCompiler</c> to decide:
    /// <list type="bullet">
    ///   <item>Whether to pre-compile expressions into cached delegates
    ///         (<see cref="SupportsCompilation"/>).</item>
    ///   <item>Whether to infer the CLR return type of an expression at
    ///         compile time (<see cref="SupportsTyping"/>).</item>
    /// </list>
    /// When <see cref="SupportsCompilation"/> is <c>false</c> the compiler
    /// wraps the engine's <c>EvaluateRaw</c> call in a captured closure;
    /// when <c>true</c> it requests a fully compiled
    /// <c>Func&lt;object?, object?&gt;</c> delegate directly.
    /// </remarks>
    public readonly struct EngineCapabilities
    {
        /// <summary>
        /// Gets a value indicating whether the engine can pre-compile an
        /// expression string into a reusable delegate before execution begins.
        /// </summary>
        public bool SupportsCompilation { get; }

        /// <summary>
        /// Gets a value indicating whether the engine can statically infer
        /// the CLR return type of an expression without executing it.
        /// </summary>
        public bool SupportsTyping { get; }

        /// <summary>
        /// Initializes a new <see cref="EngineCapabilities"/> value.
        /// </summary>
        /// <param name="supportsCompilation">
        /// <c>true</c> if the engine supports pre-compilation to a delegate.
        /// </param>
        /// <param name="supportsTyping">
        /// <c>true</c> if the engine can infer expression return types.
        /// </param>
        public EngineCapabilities(bool supportsCompilation, bool supportsTyping)
        {
            SupportsCompilation = supportsCompilation;
            SupportsTyping      = supportsTyping;
        }

        /// <summary>
        /// Returns a <see cref="EngineCapabilities"/> with all capabilities disabled.
        /// Used as the safe default for engines that do not declare their capabilities.
        /// </summary>
        public static EngineCapabilities None => new EngineCapabilities(false, false);
    }
}
