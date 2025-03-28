namespace LanguageCore.Compiler;

public partial class StatementCompiler
{

    public static class FunctionQuery
    {
        public static FunctionQuery<TFunction, TIdentifier, TArgument> Create<TFunction, TIdentifier, TArgument>(
            TIdentifier? identifier,
            ImmutableArray<TArgument>? arguments = null,
            Func<TArgument, GeneralType?, GeneralType>? converter = null,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                Arguments = arguments,
                ArgumentCount = arguments?.Length,
                Converter = converter ?? (static (argument, required) => argument as GeneralType ?? throw new InternalExceptionWithoutContext("No argument converter passed")),
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
            };

        public static FunctionQuery<TFunction, TIdentifier, TArgument> Create<TFunction, TIdentifier, TArgument>(
            TIdentifier? identifier,
            ImmutableArray<TArgument> arguments,
            Func<TArgument, GeneralType?, GeneralType> converter,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                Arguments = arguments,
                ArgumentCount = arguments.Length,
                Converter = converter,
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
            };
    }

    public readonly struct FunctionQuery<TFunction, TIdentifier, TArgument>
        where TFunction : ITemplateable<TFunction>
    {
        public TIdentifier? Identifier { get; init; }
        public Uri? RelevantFile { get; init; }
        public ImmutableArray<TArgument>? Arguments { get; init; }
        public int? ArgumentCount { get; init; }
        public GeneralType? ReturnType { get; init; }
        public Action<CompliableTemplate<TFunction>>? AddCompilable { get; init; }
        public Func<TArgument, GeneralType?, GeneralType> Converter { get; init; }

        public string ToReadable()
        {
            string identifier = Identifier?.ToString() ?? "?";
            IEnumerable<string?>? arguments = Arguments?.Select(v => v?.ToString()) ?? (ArgumentCount.HasValue ? Enumerable.Repeat(default(string), ArgumentCount.Value) : null);
            return CompiledFunctionDefinition.ToReadable(identifier, arguments, ReturnType?.ToString());
        }

        public override string ToString() => ToReadable();
    }

    public class FunctionQueryResult<TFunction> where TFunction : notnull
    {
        public required TFunction Function { get; init; }
        public Dictionary<string, GeneralType>? TypeArguments { get; init; }
        public FunctionPerfectus Perfectus { get; init; }

        public void Deconstruct(
            out TFunction function)
        {
            function = Function;
        }

        public void Deconstruct(
            out TFunction function,
            out Dictionary<string, GeneralType>? typeArguments)
        {
            function = Function;
            typeArguments = TypeArguments;
        }

        public void Deconstruct(
            out TFunction function,
            out Dictionary<string, GeneralType>? typeArguments,
            out FunctionPerfectus perfectus)
        {
            function = Function;
            typeArguments = TypeArguments;
            perfectus = Perfectus;
        }

        public override string? ToString() => Function.ToString();
    }

    public enum FunctionPerfectus
    {
        None,

        /// <summary>
        /// Both function's identifier is the same
        /// </summary>
        Identifier,

        /// <summary>
        /// Both function has the same number of parameters
        /// </summary>
        PartialParameterCount,

        /// <summary>
        /// Both function has the same number of parameters
        /// </summary>
        PerfectParameterCount,

        /// <summary>
        /// All the parameter types are almost the same
        /// </summary>
        ParameterTypes,

        /// <summary>
        /// Boundary between good and bad functions
        /// </summary>
        Good,

        // == MATCHED --> Searching for the most relevant function ==

        /// <summary>
        /// Return types are almost the same
        /// </summary>
        ReturnType,

        /// <summary>
        /// All the parameter types are the same
        /// </summary>
        PerfectParameterTypes,

        /// <summary>
        /// Return types are the same
        /// </summary>
        PerfectReturnType,

        /// <summary>
        /// All the parameter types are the same
        /// </summary>
        VeryPerfectParameterTypes,

        /// <summary>
        /// Return types are the same
        /// </summary>
        VeryPerfectReturnType,

        /// <summary>
        /// Both function are in the same file
        /// </summary>
        File,
    }

    public enum TypeMatch
    {
        None,
        Promotion,
        ImplicitCast,
        Same,
        Equals,
    }

    struct FunctionMatch<TFunction> :
        IComparable<FunctionMatch<TFunction>>,
        IEquatable<FunctionMatch<TFunction>>
        where TFunction : notnull
    {
        public required TFunction Function { get; init; }
        public required List<PossibleDiagnostic> Errors { get; init; }

        public bool IsIdentifierMatches { get; set; }
        public bool IsFileMatches { get; set; }
        public bool IsParameterCountMatches { get; set; }

        public TypeMatch ReturnTypeMatch { get; set; }
        public int UsedUpDefaultParameterValues { get; set; }
        public TypeMatch? ParameterTypeMatch { get; set; }

        public Dictionary<string, GeneralType>? TypeArguments { get; set; }

        const int Better = -1;
        const int Same = 0;
        const int Worse = 1;

        public readonly int CompareTo(FunctionMatch<TFunction> other)
        {
            if (this.Equals(other)) return Same;

            if (IsIdentifierMatches && !other.IsIdentifierMatches) return Better;
            if (!IsIdentifierMatches && other.IsIdentifierMatches) return Worse;
            if (!IsIdentifierMatches || !other.IsIdentifierMatches) return Same;

            if (IsParameterCountMatches && !other.IsParameterCountMatches) return Better;
            if (!IsParameterCountMatches && other.IsParameterCountMatches) return Worse;
            if (!IsParameterCountMatches || !other.IsParameterCountMatches) return Same;

            if (ParameterTypeMatch is not null && other.ParameterTypeMatch is not null)
            {
                TypeMatch a = ParameterTypeMatch.Value;
                TypeMatch b = other.ParameterTypeMatch.Value;
                if (a > b) return Better;
                if (a < b) return Worse;
                if (a == TypeMatch.None || b == TypeMatch.None) return Same;
            }

            if (ReturnTypeMatch > other.ReturnTypeMatch) return Better;
            if (ReturnTypeMatch < other.ReturnTypeMatch) return Worse;
            if (ReturnTypeMatch == TypeMatch.None || other.ReturnTypeMatch == TypeMatch.None) return Same;

            if (TypeArguments is null && other.TypeArguments is not null) return Better;
            if (TypeArguments is not null && other.TypeArguments is null) return Worse;

            if (UsedUpDefaultParameterValues < other.UsedUpDefaultParameterValues) return Better;
            if (UsedUpDefaultParameterValues > other.UsedUpDefaultParameterValues) return Worse;

            if (IsFileMatches && !other.IsFileMatches) return Better;
            if (!IsFileMatches && other.IsFileMatches) return Worse;

            return Same;
        }

        public override readonly string? ToString() => Function.ToString();

        public readonly bool Equals(FunctionMatch<TFunction> match)
        {
            if (IsIdentifierMatches != match.IsIdentifierMatches) return false;
            if (IsFileMatches != match.IsFileMatches) return false;
            if (IsParameterCountMatches != match.IsParameterCountMatches) return false;
            if (ReturnTypeMatch != match.ReturnTypeMatch) return false;
            if (UsedUpDefaultParameterValues != match.UsedUpDefaultParameterValues) return false;
            if ((ParameterTypeMatch is null) != (match.ParameterTypeMatch is null)) return false;
            if (ParameterTypeMatch is null || match.ParameterTypeMatch is null) return false;
            if (ParameterTypeMatch.Value != match.ParameterTypeMatch.Value) return false;
            if ((TypeArguments is null) != (match.TypeArguments is null)) return false;
            if (TypeArguments is null || match.TypeArguments is null) return false;
            if (!Utils.SequenceEquals(TypeArguments, match.TypeArguments, (a, b) => a.Key == b.Key && a.Value.Equals(b.Value))) return false;
            return true;
        }
    }

    public static bool GetFunction<TFunction, TDefinedIdentifier, TPassedIdentifier, TArgument>(
        Functions<TFunction> functions,
        string kindName,
        string? readableName,

        FunctionQuery<TFunction, TPassedIdentifier, TArgument> query,

        [NotNullWhen(true)] out FunctionQueryResult<TFunction>? result,
        [NotNullWhen(false)] out PossibleDiagnostic? error)
        where TFunction : ICompiledFunctionDefinition, IInFile, ITemplateable<TFunction>, ISimpleReadable, IIdentifiable<TDefinedIdentifier>
        where TDefinedIdentifier : notnull, IEquatable<TPassedIdentifier>
        where TArgument : notnull
    {
        string kindNameLower = kindName.ToLowerInvariant();
        string kindNameCapital = char.ToUpperInvariant(kindName[0]) + kindName[1..];

        List<FunctionMatch<TFunction>> functionMatches = new();

        foreach (TFunction function in functions.Compiled)
        {
            functionMatches.AddSorted(GetFunctionMatch<TFunction, TDefinedIdentifier, TPassedIdentifier, TArgument>(function, query));
            if (functionMatches.Count > 2) functionMatches.RemoveAt(2);
        }

        FunctionMatch<TFunction> best;

        if (query.Arguments.HasValue)
        {
            GeneralType[] argumentTypes = new GeneralType[query.Arguments.Value.Length];
            for (int i = 0; i < query.Arguments.Value.Length; i++)
            {
                argumentTypes[i] = query.Converter.Invoke(query.Arguments.Value[i], null);
            }
            FunctionQuery<TFunction, TPassedIdentifier, GeneralType> typeConvertedQuery = new()
            {
                AddCompilable = query.AddCompilable,
                ArgumentCount = query.ArgumentCount,
                Arguments = argumentTypes.ToImmutableArray(),
                Converter = (v, _) => v,
                Identifier = query.Identifier,
                RelevantFile = query.RelevantFile,
                ReturnType = query.ReturnType,
            };
            readableName = typeConvertedQuery.ToReadable() ?? readableName;
        }
        else
        {
            readableName = query.ToReadable() ?? readableName;
        }

        if (functionMatches.Count > 0)
        {
            best = functionMatches[0];
            result = new FunctionQueryResult<TFunction>()
            {
                Function = best.Function,
                Perfectus = FunctionPerfectus.File,
                TypeArguments = best.TypeArguments,
            };

            if (best.Errors.Count > 0)
            {
                error = new PossibleDiagnostic($"{kindNameCapital} \"{readableName}\" not found", best.Errors.ToArray());
                return false;
            }

            if (functionMatches.Count > 1)
            {
                if (functionMatches[0].CompareTo(functionMatches[1]) == 0)
                {
                    error = new PossibleDiagnostic($"Multiple functions matched");
                    return false;
                }
            }

            if (!best.IsIdentifierMatches)
            {
                error = new PossibleDiagnostic($"No {kindName} found with name \"{query.Identifier}\"");
                return false;
            }

            if (!best.IsParameterCountMatches)
            {
                error = new PossibleDiagnostic($"{kindNameCapital} \"{readableName}\" not found", new PossibleDiagnostic($"Wrong number of arguments passed: expected {best.Function.ParameterTypes.Count} but got {query.ArgumentCount}"));
                return false;
            }

            if (best.ParameterTypeMatch is not null &&
                best.ParameterTypeMatch.Value == TypeMatch.None)
            {
                error = new PossibleDiagnostic($"{kindNameCapital} \"{readableName}\" not found", new PossibleDiagnostic($"Wrong types of arguments passed (sorry I can't tell any more info)"));
                return false;
            }

            if (best.ReturnTypeMatch == TypeMatch.None)
            {
                error = new PossibleDiagnostic($"{kindNameCapital} \"{readableName}\" not found", new PossibleDiagnostic($"Wrong return type (sorry I can't tell any more info)"));
                return false;
            }

            if (best.Function.IsTemplate)
            {
                if (best.TypeArguments is null)
                {
                    error = new PossibleDiagnostic($"{kindNameCapital} \"{readableName}\" not found", new PossibleDiagnostic($"Failed to resolve the template types"));
                    return false;
                }

                bool templateAlreadyAdded = false;
                foreach (CompliableTemplate<TFunction> item in functions.Compilable)
                {
                    if (!object.ReferenceEquals(item.OriginalFunction, best.Function)) continue;
                    if (!Utils.SequenceEquals(item.TypeArguments, best.TypeArguments, (a, b) => a.Equals(b))) continue;
                    result = new FunctionQueryResult<TFunction>()
                    {
                        Function = item.Function,
                        Perfectus = FunctionPerfectus.File,
                        TypeArguments = best.TypeArguments,
                    };
                    templateAlreadyAdded = true;
                    break;
                }

                if (!templateAlreadyAdded)
                {
                    CompliableTemplate<TFunction> template = new(best.Function, best.TypeArguments);
                    query.AddCompilable?.Invoke(template);
                    result = new FunctionQueryResult<TFunction>()
                    {
                        Function = template.Function,
                        Perfectus = FunctionPerfectus.File,
                        TypeArguments = best.TypeArguments,
                    };
                }
            }

            error = null;
            return true;
        }
        else
        {
            result = default;
            error = new PossibleDiagnostic($"There are no functions bruh");
            return false;
        }
    }

    static FunctionMatch<TFunction> GetFunctionMatch<TFunction, TDefinedIdentifier, TPassedIdentifier, TArgument>(
        TFunction function,
        FunctionQuery<TFunction, TPassedIdentifier, TArgument> query)
        where TFunction : ICompiledFunctionDefinition, IInFile, ITemplateable<TFunction>, ISimpleReadable, IIdentifiable<TDefinedIdentifier>
        where TDefinedIdentifier : notnull, IEquatable<TPassedIdentifier>
        where TArgument : notnull
    {
        FunctionMatch<TFunction> result = new()
        {
            Function = function,
            Errors = new(),
        };

        int partial = 0;
        for (int i = 0; i < function.Parameters.Count; i++)
        {
            if (function.Parameters[i].DefaultValue is null) partial = i + 1;
            else break;
        }

        if (query.Identifier is null || function.Identifier.Equals(query.Identifier))
        {
            result.IsIdentifierMatches = true;
        }
        else
        {
            result.Errors.Add(new($"Identifier \"{query.Identifier}\" does not match with \"{function.Identifier}\""));
            return result;
        }

        if (query.ArgumentCount.HasValue)
        {
            if (query.ArgumentCount.Value < partial)
            {
                result.Errors.Add(new($"Wrong number of arguments passed: expected {function.ParameterTypes.Count} but passed {query.ArgumentCount.Value}"));
                return result;
            }

            if (query.ArgumentCount.Value > function.ParameterTypes.Count)
            {
                result.Errors.Add(new($"Wrong number of arguments passed: expected {function.ParameterTypes.Count} but passed {query.ArgumentCount.Value}"));
                return result;
            }

            result.UsedUpDefaultParameterValues = function.ParameterTypes.Count - query.ArgumentCount.Value;
        }

        result.IsParameterCountMatches = true;

        if (query.RelevantFile is null ||
            function.File == query.RelevantFile)
        {
            result.IsFileMatches = true;
        }

        void GetArgumentMatch(ref TypeMatch typeMatch, GeneralType target, TArgument current, List<PossibleDiagnostic> errors)
        {
            if (typeMatch == TypeMatch.None) return;

            PossibleDiagnostic? error = null;

            if (typeMatch >= TypeMatch.ImplicitCast)
            {
                GeneralType a = query.Converter.Invoke(current, null);

                if (typeMatch >= TypeMatch.Equals && a.Equals(target))
                {
                    typeMatch = TypeMatch.Equals;
                    return;
                }

                if (typeMatch >= TypeMatch.Same && a.SameAs(target))
                {
                    typeMatch = TypeMatch.Same;
                    return;
                }

                if (typeMatch >= TypeMatch.ImplicitCast && CanCastImplicitly(a, target, null, out error))
                {
                    typeMatch = TypeMatch.ImplicitCast;
                    return;
                }
            }

            if (typeMatch >= TypeMatch.Promotion)
            {
                if (query.Converter.Invoke(current, target).SameAs(target))
                {
                    typeMatch = TypeMatch.Promotion;
                    return;
                }
            }

            if (error is not null) errors.Add(error);
            typeMatch = TypeMatch.None;
        }

        TypeMatch GetReturnTypeMatch(GeneralType target, GeneralType current, List<PossibleDiagnostic> errors)
        {
            if (current.Equals(target))
            {
                return TypeMatch.Equals;
            }
            else if (current.SameAs(target))
            {
                return TypeMatch.Same;
            }
            else if (StatementCompiler.CanCastImplicitly(current, target, null, out PossibleDiagnostic? error))
            {
                return TypeMatch.ImplicitCast;
            }
            else
            {
                errors.Add(new PossibleDiagnostic($"Return type mismatch", error));
                return TypeMatch.None;
            }
        }

        if (function.IsTemplate)
        {
            Dictionary<string, GeneralType> _typeArguments = new();

            if (!query.Arguments.HasValue)
            {
                result.ParameterTypeMatch = null;
            }
            else
            {
                if (!GeneralType.TryGetTypeParameters(function.ParameterTypes, query.Arguments.Value.Select(v => query.Converter.Invoke(v, null)), _typeArguments))
                {
                    result.Errors.Add(new($"Could not resolve the template types"));
                    return result;
                }

                result.ParameterTypeMatch = TypeMatch.Equals;
                result.TypeArguments = _typeArguments;

                int checkCount = Math.Min(function.ParameterTypes.Count, query.Arguments.Value.Length);
                for (int i = 0; i < checkCount; i++)
                {
                    GeneralType defined = GeneralType.InsertTypeParameters(function.ParameterTypes[i], _typeArguments) ?? function.ParameterTypes[i];
                    TArgument passed = query.Arguments.Value[i];
                    TypeMatch v = result.ParameterTypeMatch.Value;
                    GetArgumentMatch(ref v, defined, passed, result.Errors);
                    if (v < result.ParameterTypeMatch) result.ParameterTypeMatch = v;
                }
            }

            if (query.ReturnType is not null)
            {
                result.ReturnTypeMatch = GetReturnTypeMatch(GeneralType.InsertTypeParameters(function.Type, _typeArguments) ?? function.Type, query.ReturnType, result.Errors);
            }
            else
            {
                result.ReturnTypeMatch = TypeMatch.Equals;
            }
        }
        else
        {
            if (!query.Arguments.HasValue)
            {
                result.ParameterTypeMatch = null;
            }
            else
            {
                result.ParameterTypeMatch = TypeMatch.Equals;

                int checkCount = Math.Min(function.ParameterTypes.Count, query.Arguments.Value.Length);
                for (int i = 0; i < checkCount; i++)
                {
                    GeneralType defined = function.ParameterTypes[i];
                    TArgument passed = query.Arguments.Value[i];
                    TypeMatch v = result.ParameterTypeMatch.Value;
                    GetArgumentMatch(ref v, defined, passed, result.Errors);
                    if (v < result.ParameterTypeMatch) result.ParameterTypeMatch = v;
                }
            }

            if (query.ReturnType is not null)
            {
                result.ReturnTypeMatch = GetReturnTypeMatch(function.Type, query.ReturnType, result.Errors);
            }
            else
            {
                result.ReturnTypeMatch = TypeMatch.Equals;
            }
        }

        return result;
    }
}
