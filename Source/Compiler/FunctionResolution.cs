using LanguageCore.Parser;
using LanguageCore.Parser.Statements;

namespace LanguageCore.Compiler;

public partial class StatementCompiler
{
    public static class FunctionQuery
    {
        public static FunctionQuery<TFunction, TIdentifier, TDefinedIdentifier, GeneralType> Create<TFunction, TIdentifier, TDefinedIdentifier>(
            TIdentifier? identifier,
            ImmutableArray<GeneralType>? arguments = null,
            FunctionQueryArgumentConverter<GeneralType>? converter = null,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                Arguments = arguments,
                ArgumentCount = arguments?.Length,
                Converter = converter ?? FunctionArgumentConverter,
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
            };

        public static FunctionQuery<TFunction, TIdentifier, TDefinedIdentifier, StatementWithValue> Create<TFunction, TIdentifier, TDefinedIdentifier>(
            TIdentifier? identifier,
            ImmutableArray<StatementWithValue> arguments,
            FunctionQueryArgumentConverter<StatementWithValue> converter,
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

        public static FunctionQuery<TFunction, TIdentifier, TDefinedIdentifier, GeneralType> Create<TFunction, TIdentifier, TDefinedIdentifier>(
            TIdentifier? identifier,
            ImmutableArray<GeneralType> arguments,
            Uri? relevantFile = null,
            GeneralType? returnType = null,
            Action<CompliableTemplate<TFunction>>? addCompilable = null,
            FunctionQueryIdentifierMatcher<TIdentifier, TDefinedIdentifier>? identifierMatcher = null)
            where TFunction : ITemplateable<TFunction>
            => new()
            {
                Identifier = identifier,
                Arguments = arguments,
                ArgumentCount = arguments.Length,
                Converter = (FunctionQueryArgumentConverter<GeneralType>)FunctionArgumentConverter,
                RelevantFile = relevantFile,
                ReturnType = returnType,
                AddCompilable = addCompilable,
                IdentifierMatcher = identifierMatcher,
            };
    }

    public readonly struct FunctionQuery<TFunction, TIdentifier, TDefinedIdentifier, TArgument>
        where TFunction : ITemplateable<TFunction>
    {
        public TIdentifier? Identifier { get; init; }
        public Uri? RelevantFile { get; init; }
        public ImmutableArray<TArgument>? Arguments { get; init; }
        public int? ArgumentCount { get; init; }
        public GeneralType? ReturnType { get; init; }
        public Action<CompliableTemplate<TFunction>>? AddCompilable { get; init; }
        public FunctionQueryArgumentConverter<TArgument> Converter { get; init; }
        public FunctionQueryIdentifierMatcher<TIdentifier, TDefinedIdentifier>? IdentifierMatcher { get; init; }

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
        public bool Success { get; init; }

        public void Deconstruct(
            out TFunction function,
            out Dictionary<string, GeneralType>? typeArguments)
        {
            function = Function;
            typeArguments = TypeArguments;
        }

        public override string? ToString() => Function.ToString();
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

        public bool IsIdentifierMatched { get; set; }
        public int IdentifierBadness { get; set; }
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

            if (IsIdentifierMatched && !other.IsIdentifierMatched) return Better;
            if (!IsIdentifierMatched && other.IsIdentifierMatched) return Worse;

            if (IdentifierBadness < other.IdentifierBadness) return Better;
            if (IdentifierBadness > other.IdentifierBadness) return Worse;
            if (!IsIdentifierMatched || !other.IsIdentifierMatched) return Same;

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
            if (IdentifierBadness != match.IdentifierBadness) return false;
            if (IsIdentifierMatched != match.IsIdentifierMatched) return false;
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

    public delegate bool FunctionQueryArgumentConverter<TArgument>(
        TArgument passed,
        ParameterDefinition? definition,
        GeneralType? defined,
        [NotNullWhen(true)] out GeneralType? result);

    public delegate bool FunctionQueryIdentifierMatcher<TIdentifier, TDefinedIdentifier>(
        TIdentifier passed,
        TDefinedIdentifier defined,
        out int badness);

    static bool FunctionArgumentConverter(
        GeneralType argument,
        ParameterDefinition? parameterDefinition,
        GeneralType? expectedType,
        [NotNullWhen(true)] out GeneralType? result)
    {
        result = argument;
        return true;
    }

    bool FunctionArgumentConverter(
        StatementWithValue argument,
        ParameterDefinition? parameterDefinition,
        GeneralType? expectedType,
        [NotNullWhen(true)] out GeneralType? result)
    {
        if (expectedType is not null)
        {
            // if (expectedType.Is<PointerType>() &&
            //     parameterDefinition.Modifiers.Any(v => v.Content == ModifierKeywords.This) &&
            //     !FindStatementType(argument).Is<PointerType>())
            // {
            //     argument = new AddressGetter(
            //         Tokenizing.Token.CreateAnonymous("&", Tokenizing.TokenType.Operator, argument.Position.Before()),
            //         argument,
            //         argument.File
            //     );
            // }

            if (!expectedType.AllGenericsDefined())
            {
                expectedType = null;
            }
        }

        result = FindStatementType(argument, expectedType);
        return true;
    }

    public static bool GetFunction<TFunction, TPassedIdentifier, TDefinedIdentifier, TArgument>(
        Functions<TFunction> functions,
        string kindName,
        string? readableName,

        FunctionQuery<TFunction, TPassedIdentifier, TDefinedIdentifier, TArgument> query,

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

        readableName = query.ToReadable() ?? readableName;
        if (query.Arguments.HasValue)
        {
            GeneralType[] argumentTypes = new GeneralType[query.Arguments.Value.Length];
            for (int i = 0; i < query.Arguments.Value.Length; i++)
            {
                if (!query.Converter.Invoke(query.Arguments.Value[i], null, null, out GeneralType? converted))
                {
                    goto bad;
                }
                argumentTypes[i] = converted;
            }
            FunctionQuery<TFunction, TPassedIdentifier, TDefinedIdentifier, GeneralType> typeConvertedQuery = new()
            {
                AddCompilable = query.AddCompilable,
                ArgumentCount = query.ArgumentCount,
                Arguments = argumentTypes.ToImmutableArray(),
                Converter = FunctionArgumentConverter,
                Identifier = query.Identifier,
                RelevantFile = query.RelevantFile,
                ReturnType = query.ReturnType,
            };
            readableName = typeConvertedQuery.ToReadable() ?? readableName;
        bad:;
        }

        if (functionMatches.Count > 0)
        {
            best = functionMatches[0];
            result = new FunctionQueryResult<TFunction>()
            {
                Function = best.Function,
                Success = true,
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

            if (!best.IsIdentifierMatched)
            {
                if (best.IdentifierBadness == 1)
                {
                    error = new PossibleDiagnostic($"No {kindName} found with name \"{query.Identifier}\" (did you mean \"{best.Function.Identifier}\"?)");
                }
                else
                {
                    error = new PossibleDiagnostic($"No {kindName} found with name \"{query.Identifier}\"");
                }
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
                        Success = true,
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
                        Success = true,
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
        FunctionQuery<TFunction, TPassedIdentifier, TDefinedIdentifier, TArgument> query)
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

        if (query.Identifier is null)
        {
            result.IsIdentifierMatched = true;
            result.IdentifierBadness = 0;
        }
        else if (query.IdentifierMatcher is not null)
        {
            if (query.IdentifierMatcher.Invoke(query.Identifier, function.Identifier, out int identifierBadness))
            {
                result.IsIdentifierMatched = true;
                result.IdentifierBadness = identifierBadness;
            }
            else
            {
                result.IsIdentifierMatched = false;
            }
        }
        else if (function.Identifier.Equals(query.Identifier))
        {
            result.IsIdentifierMatched = true;
            result.IdentifierBadness = 0;
        }
        else
        {
            result.IsIdentifierMatched = false;
            result.IdentifierBadness = 2;

            if (query.Identifier is string _a1 &&
                function.Identifier is Tokenizing.Token _b1)
            {
                if (_a1.ToLowerInvariant() == _b1.Content.ToLowerInvariant())
                {
                    result.IdentifierBadness = 1;
                }
            }

            result.Errors.Add(new($"Function \"{query.Identifier}\" does not match with \"{function.Identifier}\""));
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

        void GetArgumentMatch(ref TypeMatch typeMatch, GeneralType definedType, Parser.ParameterDefinition definition, TArgument passed, List<PossibleDiagnostic> errors)
        {
            if (typeMatch == TypeMatch.None) return;

            PossibleDiagnostic? error = null;

            if (typeMatch >= TypeMatch.ImplicitCast)
            {
                if (!query.Converter.Invoke(passed, definition, null, out GeneralType? a))
                {
                    typeMatch = TypeMatch.None;
                    return;
                }

                if (typeMatch >= TypeMatch.Equals && a.Equals(definedType))
                {
                    typeMatch = TypeMatch.Equals;
                    return;
                }

                if (typeMatch >= TypeMatch.Same && a.SameAs(definedType))
                {
                    typeMatch = TypeMatch.Same;
                    return;
                }

                if (typeMatch >= TypeMatch.ImplicitCast && CanCastImplicitly(a, definedType, null, out error))
                {
                    typeMatch = TypeMatch.ImplicitCast;
                    return;
                }
            }

            if (typeMatch >= TypeMatch.Promotion)
            {
                if (query.Converter.Invoke(passed, definition, definedType, out GeneralType? converted))
                {
                    if (converted.SameAs(definedType))
                    {
                        typeMatch = TypeMatch.Promotion;
                        return;
                    }
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
                int checkCount = Math.Min(function.ParameterTypes.Count, query.Arguments.Value.Length);

                for (int i = 0; i < checkCount; i++)
                {
                    GeneralType defined = function.ParameterTypes[i];
                    if (!query.Converter.Invoke(query.Arguments.Value[i], function.Parameters[i], defined, out GeneralType? passed))
                    {
                        result.Errors.Add(new PossibleDiagnostic($"Could not resolve the template types"));
                        return result;
                    }

                    if (!GeneralType.TryGetTypeParameters(defined, passed, _typeArguments))
                    {
                        result.Errors.Add(new PossibleDiagnostic($"Could not resolve the template types",
                            new PossibleDiagnostic($"Invalid type passed: expected {GeneralType.InsertTypeParameters(defined, _typeArguments) ?? defined} but passed {passed}")));
                        return result;
                    }
                }

                result.ParameterTypeMatch = TypeMatch.Equals;
                result.TypeArguments = _typeArguments;

                for (int i = 0; i < checkCount; i++)
                {
                    GeneralType defined = GeneralType.InsertTypeParameters(function.ParameterTypes[i], _typeArguments) ?? function.ParameterTypes[i];
                    TArgument passed = query.Arguments.Value[i];
                    TypeMatch v = result.ParameterTypeMatch.Value;
                    GetArgumentMatch(ref v, defined, function.Parameters[i], passed, result.Errors);
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
                    GetArgumentMatch(ref v, defined, function.Parameters[i], passed, result.Errors);
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
