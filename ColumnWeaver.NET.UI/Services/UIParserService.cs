using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ColumnWeaver.NET.Application.Engine;
using ColumnWeaver.NET.Application.Factory;
using ColumnWeaver.NET.Application.Validation;
using ColumnWeaver.NET.Core.Columns;
using ColumnWeaver.NET.Core.Interfaces;
using ColumnWeaver.NET.Core.Models;
using ColumnWeaver.NET.Core.ParserConfiguration;
using ColumnWeaver.NET.Infrastructure.PropertyResolution;
using ColumnWeaver.NET.Infrastructure.Serialization;

namespace ColumnWeaver.NET.UI.Services
{
    /// <summary>
    /// Presentation-layer façade that mediates between the WinForms UI and the
    /// Application layer <see cref="ParserEngine"/>.
    /// </summary>
    /// <remarks>
    /// <b>Responsibilities:</b>
    /// <list type="bullet">
    ///   <item>Owns the current <see cref="ParserConfiguration"/> instance.</item>
    ///   <item>Exposes simple column-CRUD operations that delegate state changes to the config.</item>
    ///   <item>Raises <see cref="ConfigurationChanged"/> after every mutation so the UI can refresh.</item>
    ///   <item>Provides <see cref="TryPreviewParse"/> for the live-preview panel using the production engine.</item>
    ///   <item>Provides <see cref="TryTestParse"/> for the test-input panel using a <see cref="JObject"/>-backed sandbox engine.</item>
    /// </list>
    /// <b>No business logic lives here.</b> All evaluation is delegated to <see cref="ParserEngine"/>.
    /// </remarks>
    public sealed class UIParserService
    {
        // -----------------------------------------------------------------
        //  Infrastructure dependencies
        // -----------------------------------------------------------------

        private readonly ParserEngine               _engine;
        private readonly CompiledParserEngine        _compiledEngine;
        private readonly IExpressionEngine          _expressionEngine;
        private readonly IConfigurationSerializer   _jsonSerializer;
        private readonly IConfigurationSerializer   _xmlSerializer;
        private readonly Application.Abstractions.ICustomFunctionRegistry? _functionRegistry;

        // -----------------------------------------------------------------
        //  State
        // -----------------------------------------------------------------

        /// <summary>Gets the configuration currently being edited.</summary>
        public ParserConfiguration CurrentConfiguration { get; private set; }

        /// <summary>Gets the file path most recently used to save/load, or null.</summary>
        public string? CurrentFilePath { get; private set; }

        /// <summary>Gets whether the configuration has unsaved changes.</summary>
        public bool IsModified { get; private set; }

        /// <summary>
        /// Gets the CLR object currently used as the source for the live preview panel.
        /// Set via <see cref="SetPreviewSource"/>.
        /// </summary>
        public object? PreviewSource { get; private set; }

        private readonly Stack<string> _undoStack = new Stack<string>();
        private readonly Stack<string> _redoStack = new Stack<string>();
        private string? _clipboardJson;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Raised after ANY change to <see cref="CurrentConfiguration"/>
        /// (columns added/removed/updated, global settings changed, load/new).
        /// </summary>
        public event EventHandler? ConfigurationChanged;

        // -----------------------------------------------------------------
        //  Constructor
        // -----------------------------------------------------------------

        /// <summary>
        /// Initializes the service with the provided Application and Infrastructure dependencies,
        /// explicitly providing both the dynamic fallback engine and the compiled production engine.
        /// </summary>
        public UIParserService(
            ParserEngine             engine,
            CompiledParserEngine     compiledEngine,
            IExpressionEngine        expressionEngine,
            IConfigurationSerializer jsonSerializer,
            IConfigurationSerializer xmlSerializer,
            Application.Abstractions.ICustomFunctionRegistry? functionRegistry = null)
        {
            _engine           = engine           ?? throw new ArgumentNullException(nameof(engine));
            _compiledEngine   = compiledEngine   ?? throw new ArgumentNullException(nameof(compiledEngine));
            _expressionEngine = expressionEngine ?? throw new ArgumentNullException(nameof(expressionEngine));
            _jsonSerializer   = jsonSerializer   ?? throw new ArgumentNullException(nameof(jsonSerializer));
            _xmlSerializer    = xmlSerializer    ?? throw new ArgumentNullException(nameof(xmlSerializer));
            _functionRegistry = functionRegistry;

            CurrentConfiguration = CreateDefault();
        }

        /// <summary>
        /// Initializes the service with the provided Application and Infrastructure dependencies.
        /// Builds a <see cref="CompiledParserEngine"/> using the provided expression engine.
        /// </summary>
        public UIParserService(
            ParserEngine             engine,
            IExpressionEngine        expressionEngine,
            IConfigurationSerializer jsonSerializer,
            IConfigurationSerializer xmlSerializer,
            Application.Abstractions.ICustomFunctionRegistry? functionRegistry = null)
            : this(
                engine,
                new CompiledParserEngine(new ReflectionPropertyResolver(), expressionEngine, functionRegistry),
                expressionEngine,
                jsonSerializer,
                xmlSerializer,
                functionRegistry)
        {
        }

        // -----------------------------------------------------------------
        //  File operations
        // -----------------------------------------------------------------

        /// <summary>Resets to a blank <see cref="ParserConfiguration"/>.</summary>
        public void New()
        {
            PushUndoState();
            CurrentConfiguration = CreateDefault();
            CurrentFilePath      = null;
            IsModified           = false;
            RaiseConfigurationChanged();
        }

        /// <summary>Loads a configuration from a JSON or XML file.</summary>
        public void Load(string filePath, bool isXml = false)
        {
            PushUndoState();
            var serializer       = isXml ? _xmlSerializer : _jsonSerializer;
            CurrentConfiguration = serializer.DeserializeFromFile(filePath);
            CurrentFilePath      = filePath;
            IsModified           = false;
            RaiseConfigurationChanged();
        }

        /// <summary>
        /// Saves the current configuration. Uses the last-known path when
        /// <paramref name="filePath"/> is null.
        /// </summary>
        public void Save(string? filePath = null, bool isXml = false)
        {
            var path = filePath ?? CurrentFilePath
                ?? throw new InvalidOperationException(
                    "No file path provided and no previous save path is known.");

            var serializer  = isXml ? _xmlSerializer : _jsonSerializer;
            serializer.SerializeToFile(CurrentConfiguration, path);
            CurrentFilePath = path;
            IsModified      = false;
        }

        // -----------------------------------------------------------------
        //  Global configuration settings
        // -----------------------------------------------------------------

        /// <summary>Updates the configuration name and notifies listeners.</summary>
        public void SetName(string name)
        {
            if (CurrentConfiguration.Name == name) return;
            PushUndoState();
            CurrentConfiguration.Name = name;
            SetModified();
        }

        /// <summary>Updates the global column separator and notifies listeners.</summary>
        public void SetGlobalSeparator(string separator)
        {
            if (CurrentConfiguration.GlobalSeparator == separator) return;
            PushUndoState();
            CurrentConfiguration.GlobalSeparator = separator;
            SetModified();
        }

        /// <summary>Toggles whether disabled columns still occupy a separator slot.</summary>
        public void SetPreserveSeparator(bool value)
        {
            if (CurrentConfiguration.PreserveSeparatorForDisabledColumns == value) return;
            PushUndoState();
            CurrentConfiguration.PreserveSeparatorForDisabledColumns = value;
            SetModified();
        }

        /// <summary>Updates the global variables dictionary and notifies listeners.</summary>
        public void UpdateVariables(Dictionary<string, object?> newVariables)
        {
            PushUndoState();
            CurrentConfiguration.Variables = newVariables ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            SetModified();
        }

        // -----------------------------------------------------------------
        //  Column CRUD
        // -----------------------------------------------------------------

        /// <summary>
        /// Adds a fully constructed <see cref="ColumnDefinition"/> to the configuration.
        /// Assigns the next available order index automatically.
        /// </summary>
        public void AddColumn(ColumnDefinition column)
        {
            if (column == null) throw new ArgumentNullException(nameof(column));
            PushUndoState();
            column.Order = CurrentConfiguration.Columns.Count;
            CurrentConfiguration.Columns.Add(column);
            SetModified();
        }

        /// <summary>Removes the column with the given <paramref name="columnId"/>.</summary>
        public bool RemoveColumn(Guid columnId)
        {
            var col = FindColumn(columnId);
            if (col == null) return false;

            PushUndoState();
            CurrentConfiguration.Columns.Remove(col);
            ResequenceOrders();
            SetModified();
            return true;
        }

        /// <summary>
        /// Replaces the existing column that has the same <see cref="ColumnDefinition.Id"/>
        /// as <paramref name="updated"/>.
        /// </summary>
        public void UpdateColumn(ColumnDefinition updated)
        {
            if (updated == null) throw new ArgumentNullException(nameof(updated));
            var idx = IndexOfColumn(updated.Id);
            if (idx < 0) return;

            PushUndoState();
            CurrentConfiguration.Columns[idx] = updated;
            SetModified();
        }

        /// <summary>Moves the column one position earlier in the output sequence.</summary>
        public void MoveColumnUp(Guid columnId)
        {
            var col = FindColumn(columnId);
            if (col == null || col.Order == 0) return;

            var prev = FindByOrder(col.Order - 1);
            if (prev != null) prev.Order++;

            PushUndoState();
            col.Order--;
            SetModified();
        }

        public void MoveColumnDown(Guid columnId)
        {
            var col  = FindColumn(columnId);
            if (col == null) return;

            var next = FindByOrder(col.Order + 1);
            if (next == null) return;

            PushUndoState();
            next.Order--;
            col.Order++;
            SetModified();
        }

        /// <summary>Moves a column to a specific index in the output sequence.</summary>
        public void MoveColumnToIndex(Guid columnId, int newIndex)
        {
            var col = FindColumn(columnId);
            if (col == null) return;

            var oldIndex = IndexOfColumn(columnId);
            if (oldIndex < 0 || oldIndex == newIndex) return;

            PushUndoState();
            
            CurrentConfiguration.Columns.RemoveAt(oldIndex);
            
            if (newIndex > oldIndex) {
                 newIndex--; 
            }
            
            newIndex = Math.Max(0, Math.Min(newIndex, CurrentConfiguration.Columns.Count));
            CurrentConfiguration.Columns.Insert(newIndex, col);

            ResequenceOrders();
            SetModified();
        }

        /// <summary>Enables or disables the column.</summary>
        public void SetColumnEnabled(Guid columnId, bool enabled)
        {
            var col = FindColumn(columnId);
            if (col == null || col.IsEnabled == enabled) return;
            PushUndoState();
            col.IsEnabled = enabled;
            SetModified();
        }

        // -----------------------------------------------------------------
        //  Parse operations
        // -----------------------------------------------------------------

        /// <summary>
        /// Parses using the compiled production engine with a pre-built CLR source object.
        /// Uses <see cref="CompiledParserEngine"/> with compiled expression delegates and NCalc fallback,
        /// matching the behavior of the public <see cref="ColumnWeaverClient"/>.
        /// Returns the output string, or an error message prefixed with "⚠ ".
        /// </summary>
        public string TryPreviewParse(object? source)
        {
            try
            {
                return _compiledEngine.Parse(CurrentConfiguration, source, source?.GetType());
            }
            catch (Exception ex)
            {
                return $"⚠  {ex.Message}";
            }
        }

        /// <summary>
        /// Parses using a JSON string pasted by the user in the test-input panel.
        /// Internally builds a dynamic sandbox <see cref="ParserEngine"/> backed by
        /// <see cref="JObjectPropertyResolver"/> and dynamic NCalc evaluation so the user does not need a real CLR type.
        /// Returns the output string, or an error message prefixed with "⚠ ".
        /// </summary>
        public string TryTestParse(string json)
        {
            try
            {
                JObject source;
                try { source = JObject.Parse(json); }
                catch (Exception ex)
                {
                    return $"⚠  Invalid JSON: {ex.Message}";
                }

                // Build a lightweight dynamic sandbox engine backed by JObjectPropertyResolver and dynamic NCalc.
                var sandboxResolver = new JObjectPropertyResolver();
                var sandboxExprEngine = new ColumnWeaver.NET.Infrastructure.ExpressionEngine.NCalcExpressionEngine(sandboxResolver);
                var sandboxFactory  = new ColumnEvaluatorFactory(sandboxResolver, sandboxExprEngine, _functionRegistry);
                var sandboxEngine   = new ParserEngine(sandboxFactory);

                return sandboxEngine.Parse(CurrentConfiguration, source);
            }
            catch (Exception ex)
            {
                return $"⚠  {ex.Message}";
            }
        }

        /// <summary>Validates a NCalc expression via the expression engine.</summary>
        public bool ValidateExpression(string expression)
            => _expressionEngine.ValidateExpression(expression);

        // ── V2: Structured validation ──────────────────────────────────────────

        /// <summary>
        /// Validates the current configuration using the full V2
        /// <see cref="ConfigurationValidator"/> and returns a structured
        /// <see cref="ValidationResult"/> with all errors and warnings.
        /// </summary>
        /// <param name="sourceType">
        /// Optional CLR type for property-path validation.
        /// Pass <c>null</c> to skip property-path checks (design-time mode).
        /// </param>
        public ValidationResult ValidateCurrentConfiguration(Type? sourceType = null)
        {
            return ConfigurationValidator.Validate(
                CurrentConfiguration,
                sourceType,
                new ReflectionPropertyResolver(),
                _expressionEngine,
                _functionRegistry);
        }

        // ── V2: Registry function list (for UI dropdown) ────────────────────────

        /// <summary>
        /// Returns the names of all registered custom functions, or an empty list
        /// when no function registry was provided at construction.
        /// Used by <c>CustomFunctionColumnEditorControl</c> to populate its
        /// function-name dropdown.
        /// </summary>
        public IReadOnlyList<string> GetRegisteredFunctions()
            => _functionRegistry?.GetRegisteredFunctionNames()
                ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Returns the <see cref="FunctionMetadata"/> for a registered function,
        /// or <c>null</c> when the function is not registered or no registry was
        /// provided.
        /// </summary>
        public FunctionMetadata? GetFunctionMetadata(string functionName)
            => _functionRegistry?.GetMetadata(functionName);

        // ── V2: Batch parsing ───────────────────────────────────────────────────

        /// <summary>
        /// Parses a sequence of source objects using the compiled V2 execution engine.
        /// The plan is compiled once and reused across all items.
        /// </summary>
        /// <param name="inputs">The sequence of source objects to parse.</param>
        /// <param name="sourceType">Optional CLR type for compile-time type inference.</param>
        /// <returns>An array of output strings, one per input item.</returns>
        public string[] ParseMany(IEnumerable<object?> inputs, Type? sourceType = null)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));

            var results = new List<string>();
            foreach (var output in _compiledEngine.ParseMany(
                CurrentConfiguration, inputs, sourceType))
            {
                results.Add(output);
            }
            return results.ToArray();
        }

        // -----------------------------------------------------------------
        //  Configuration loading
        // -----------------------------------------------------------------

        /// <summary>
        /// Replaces the current configuration with a pre-built instance
        /// (e.g., a demo configuration loaded at startup).
        /// Does NOT set <see cref="IsModified"/>.
        /// </summary>
        public void LoadConfiguration(ParserConfiguration configuration)
        {
            CurrentConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            CurrentFilePath      = null;
            IsModified           = false;
            RaiseConfigurationChanged();
        }

        // -----------------------------------------------------------------
        //  Live preview source
        // -----------------------------------------------------------------

        /// <summary>
        /// Sets the CLR object used as input by the live preview panel.
        /// Raises <see cref="ConfigurationChanged"/> so the preview refreshes immediately.
        /// </summary>
        public void SetPreviewSource(object? source)
        {
            PreviewSource = source;
            RaiseConfigurationChanged();
        }

        /// <summary>
        /// Parses the current configuration against the stored
        /// <see cref="PreviewSource"/> (set via <see cref="SetPreviewSource"/>).
        /// Returns an error message prefixed with "\u26a0 " on failure.
        /// </summary>
        public string TryPreviewParse()
            => TryPreviewParse(PreviewSource);

        // -----------------------------------------------------------------
        //  Undo / Redo / Clipboard Operations
        // -----------------------------------------------------------------

        private void PushUndoState()
        {
            _undoStack.Push(_jsonSerializer.Serialize(CurrentConfiguration));
            _redoStack.Clear(); // Any new action invalidates future redos
        }

        public void Undo()
        {
            if (!CanUndo) return;
            _redoStack.Push(_jsonSerializer.Serialize(CurrentConfiguration));
            var prevJson = _undoStack.Pop();
            CurrentConfiguration = _jsonSerializer.Deserialize(prevJson);
            IsModified = true;
            RaiseConfigurationChanged();
        }

        public void Redo()
        {
            if (!CanRedo) return;
            _undoStack.Push(_jsonSerializer.Serialize(CurrentConfiguration));
            var nextJson = _redoStack.Pop();
            CurrentConfiguration = _jsonSerializer.Deserialize(nextJson);
            IsModified = true;
            RaiseConfigurationChanged();
        }

        public bool CopyColumn(Guid id)
        {
            var col = FindColumn(id);
            if (col == null) return false;

            // Use JSON to deep copy precisely
            var tempConfig = new ParserConfiguration { Columns = { col } };
            _clipboardJson = _jsonSerializer.Serialize(tempConfig);
            return true;
        }

        public bool CutColumn(Guid id)
        {
            if (CopyColumn(id))
            {
                RemoveColumn(id);
                return true;
            }
            return false;
        }

        public ColumnDefinition? PasteColumn()
        {
            if (string.IsNullOrEmpty(_clipboardJson)) return null;

            try
            {
                var tempConfig = _jsonSerializer.Deserialize(_clipboardJson!);
                var pastedCol = tempConfig.Columns.FirstOrDefault();
                if (pastedCol == null) return null;

                pastedCol.Id = Guid.NewGuid();
                
                // Avoid name duplication validation errors
                if (!string.IsNullOrEmpty(pastedCol.Name)) pastedCol.Name += "_Copy";
                if (!string.IsNullOrEmpty(pastedCol.Alias)) pastedCol.Alias += "_Copy";

                AddColumn(pastedCol); // Handles Undo/Modified internally
                return pastedCol;
            }
            catch
            {
                return null;
            }
        }

        // -----------------------------------------------------------------
        //  Private helpers
        // -----------------------------------------------------------------

        private static ParserConfiguration CreateDefault()
            => new ParserConfiguration { Name = "New Configuration", GlobalSeparator = "|" };

        private ColumnDefinition? FindColumn(Guid id)
            => CurrentConfiguration.Columns.FirstOrDefault(c => c?.Id == id);

        private int IndexOfColumn(Guid id)
        {
            for (int i = 0; i < CurrentConfiguration.Columns.Count; i++)
                if (CurrentConfiguration.Columns[i]?.Id == id) return i;
            return -1;
        }

        private ColumnDefinition? FindByOrder(int order)
            => CurrentConfiguration.Columns.FirstOrDefault(c => c?.Order == order);

        private void ResequenceOrders()
        {
            int order = 0;
            foreach (var col in CurrentConfiguration.Columns
                .Where(c => c != null)
                .OrderBy(c => c!.Order))
                col!.Order = order++;
        }

        private void SetModified()
        {
            IsModified = true;
            RaiseConfigurationChanged();
        }

        private void RaiseConfigurationChanged()
            => ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }
}
