using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Supercent.PlayableAI.Common.Contracts;
using Supercent.PlayableAI.Common.Format;

namespace Supercent.PlayableAI.Generation.Editor.Validation
{
    public static class FeatureOptionsSchemaValidator
    {
        private enum JsonTokenKind
        {
            Unknown = 0,
            Object,
            Array,
            String,
            Number,
            Bool,
            Null,
        }

        private sealed class JsonToken
        {
            public JsonTokenKind Kind;
            public int Start;
            public int End;
            public string Raw = string.Empty;
            public string StringValue = string.Empty;
        }

        public static void ValidateCompiledOptions(
            PlayableScenarioFeatureOptionDefinition definition,
            FeatureDescriptor descriptor,
            Dictionary<string, CompiledSpawnData> spawnLookup,
            string label,
            List<string> errors)
        {
            if (definition == null)
                return;

            string optionsJson = definition.options.optionsJson;
            ValidateOptionsJson(
                descriptor,
                optionsJson,
                label,
                targetIdExists: value => spawnLookup != null && spawnLookup.ContainsKey(value),
                itemRefIsValid: null,
                errors: errors);
        }

        public static void ValidateOptionsJson(
            FeatureDescriptor descriptor,
            string optionsJson,
            string label,
            Func<string, bool> targetIdExists,
            List<string> errors)
        {
            ValidateOptionsJson(
                descriptor,
                optionsJson,
                label,
                targetIdExists,
                itemRefIsValid: null,
                errors: errors);
        }

        public static void ValidateOptionsJson(
            FeatureDescriptor descriptor,
            string optionsJson,
            string label,
            Func<string, bool> targetIdExists,
            Func<string, string, string[], bool> itemRefIsValid,
            List<string> errors)
        {
            ValidateOptionsJson(
                descriptor,
                optionsJson,
                label,
                targetIdExists,
                itemRefIsValid,
                assetRefIsValid: null,
                errors: errors);
        }

        public static void ValidateOptionsJson(
            FeatureDescriptor descriptor,
            string optionsJson,
            string label,
            Func<string, bool> targetIdExists,
            Func<string, string, string[], bool> itemRefIsValid,
            Func<string, string, string, bool> assetRefIsValid,
            List<string> errors)
        {
            FeatureOptionFieldDescriptor[] fields = descriptor != null && descriptor.optionSchema != null
                ? descriptor.optionSchema.fields ?? new FeatureOptionFieldDescriptor[0]
                : new FeatureOptionFieldDescriptor[0];

            string trimmedJson = optionsJson != null ? optionsJson.Trim() : string.Empty;
            if (!TryParseTopLevelProperties(trimmedJson, out Dictionary<string, JsonToken> properties, out string parseError))
            {
                AddError(errors, label + ".optionsJson 파싱 실패: " + parseError);
                return;
            }

            ValidateNoUnknownProperties(fields, properties, label, errors);

            for (int i = 0; i < fields.Length; i++)
            {
                FeatureOptionFieldDescriptor field = fields[i];
                if (field == null)
                    continue;

                string fieldId = FeatureDescriptorUtility.Normalize(field.fieldId);
                if (string.IsNullOrEmpty(fieldId))
                    continue;

                string valueType = FeatureDescriptorUtility.Normalize(field.valueType);
                string context = label + ".optionsJson '" + fieldId + "'";
                if (!TryGetProperty(properties, fieldId, out JsonToken token))
                {
                    if (field.required)
                        AddError(errors, context + " 필수 옵션이 없습니다.");
                    continue;
                }

                ValidateToken(token, valueType, field.minIntValue, field.requiredItemDesignCapabilities, context, targetIdExists, itemRefIsValid, assetRefIsValid, errors);
            }
        }

        private static void ValidateToken(
            JsonToken token,
            string valueType,
            int minIntValue,
            string[] requiredItemDesignCapabilities,
            string context,
            Func<string, bool> targetIdExists,
            Func<string, string, string[], bool> itemRefIsValid,
            Func<string, string, string, bool> assetRefIsValid,
            List<string> errors)
        {
            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_ITEM_REF, StringComparison.Ordinal))
            {
                ValidateItemRefToken(token, requiredItemDesignCapabilities, context, itemRefIsValid, errors);
                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_AUDIO_CLIP_REF, StringComparison.Ordinal))
            {
                ValidateAssetRefToken(token, "UnityEngine.AudioClip", context, assetRefIsValid, errors);
                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_TARGET_OBJECT_ID, StringComparison.Ordinal))
            {
                if (token == null || token.Kind != JsonTokenKind.String)
                {
                    AddError(errors, context + "은 target object id 문자열이어야 합니다.");
                    return;
                }

                string targetId = FeatureDescriptorUtility.Normalize(token.StringValue);
                if (string.IsNullOrEmpty(targetId))
                {
                    AddError(errors, context + "이 비어 있습니다.");
                    return;
                }

                if (targetIdExists != null && !targetIdExists(targetId))
                    AddError(errors, context + "이 알 수 없는 target object id를 참조합니다: " + targetId);
                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_INT, StringComparison.Ordinal))
            {
                if (!TryReadIntToken(token, out _))
                    AddError(errors, context + "은 int 값이어야 합니다.");

                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_INT_RANGE, StringComparison.Ordinal))
            {
                ValidateIntRangeToken(token, minIntValue, context, errors);
                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_FLOAT, StringComparison.Ordinal))
            {
                string raw = GetRaw(token);
                if (token == null ||
                    token.Kind != JsonTokenKind.Number ||
                    !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    AddError(errors, context + "은 float 값이어야 합니다.");
                }

                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_STRING, StringComparison.Ordinal))
            {
                if (token == null || token.Kind != JsonTokenKind.String)
                    AddError(errors, context + "은 string 값이어야 합니다.");

                return;
            }

            if (string.Equals(valueType, FeatureDescriptorContracts.VALUE_TYPE_BOOL, StringComparison.Ordinal))
            {
                if (token == null || token.Kind != JsonTokenKind.Bool)
                    AddError(errors, context + "은 bool 값이어야 합니다.");
            }
        }

        private static void ValidateNoUnknownProperties(
            FeatureOptionFieldDescriptor[] fields,
            Dictionary<string, JsonToken> properties,
            string label,
            List<string> errors)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal);
            FeatureOptionFieldDescriptor[] safeFields = fields ?? new FeatureOptionFieldDescriptor[0];
            for (int i = 0; i < safeFields.Length; i++)
            {
                string fieldId = FeatureDescriptorUtility.Normalize(safeFields[i] != null ? safeFields[i].fieldId : string.Empty);
                if (!string.IsNullOrEmpty(fieldId))
                    allowed.Add(fieldId);
            }

            if (properties == null)
                return;

            foreach (KeyValuePair<string, JsonToken> pair in properties)
            {
                string key = FeatureDescriptorUtility.Normalize(pair.Key);
                if (!string.IsNullOrEmpty(key) && !allowed.Contains(key))
                    AddError(errors, label + ".optionsJson에 descriptor optionSchema.fields에 없는 key가 있습니다: " + key);
            }
        }

        private static void ValidateAssetRefToken(
            JsonToken token,
            string expectedAssetType,
            string context,
            Func<string, string, string, bool> assetRefIsValid,
            List<string> errors)
        {
            if (token == null || token.Kind != JsonTokenKind.Object)
            {
                AddError(errors, context + "은 asset reference object여야 합니다.");
                return;
            }

            string raw = GetRaw(token);
            if (!TryParseTopLevelProperties(raw, out Dictionary<string, JsonToken> properties, out string parseError))
            {
                AddError(errors, context + " asset reference 파싱 실패: " + parseError);
                return;
            }

            string assetGuid = ReadStringProperty(properties, "assetGuid");
            string assetPath = ReadStringProperty(properties, "assetPath");
            string assetType = ReadStringProperty(properties, "assetType");
            if (string.IsNullOrEmpty(assetGuid) && string.IsNullOrEmpty(assetPath))
            {
                AddError(errors, context + "은 assetGuid 또는 assetPath가 필요합니다.");
                return;
            }

            if (!string.IsNullOrEmpty(assetType) &&
                !string.Equals(assetType, expectedAssetType, StringComparison.Ordinal) &&
                !string.Equals(assetType, "AudioClip", StringComparison.Ordinal))
            {
                AddError(errors, context + " assetType이 audio clip이 아닙니다: " + assetType);
                return;
            }

            if (assetRefIsValid != null && !assetRefIsValid(assetGuid, assetPath, expectedAssetType))
                AddError(errors, context + " asset resolve 검증에 실패했습니다.");
        }

        private static void ValidateItemRefToken(
            JsonToken token,
            string[] requiredItemDesignCapabilities,
            string context,
            Func<string, string, string[], bool> itemRefIsValid,
            List<string> errors)
        {
            if (token == null || token.Kind != JsonTokenKind.Object)
            {
                AddError(errors, context + "은 ItemRef object여야 합니다.");
                return;
            }

            string raw = GetRaw(token);
            if (!TryParseTopLevelProperties(raw, out Dictionary<string, JsonToken> properties, out string parseError))
            {
                AddError(errors, context + " ItemRef 파싱 실패: " + parseError);
                return;
            }

            string familyId = ReadStringProperty(properties, "familyId");
            string variantId = ReadStringProperty(properties, "variantId");
            if (string.IsNullOrEmpty(familyId) || string.IsNullOrEmpty(variantId))
            {
                AddError(errors, context + "은 familyId와 variantId가 모두 필요합니다.");
                return;
            }

            if (itemRefIsValid != null &&
                !itemRefIsValid(familyId, variantId, requiredItemDesignCapabilities ?? new string[0]))
            {
                AddError(errors, context + "이 catalog item 또는 required capability 검증에 실패했습니다: " + familyId + "/" + variantId);
            }
        }

        private static void ValidateIntRangeToken(JsonToken token, int minIntValue, string context, List<string> errors)
        {
            if (token == null || token.Kind != JsonTokenKind.Object)
            {
                AddError(errors, context + "은 min/max int range object여야 합니다.");
                return;
            }

            string raw = GetRaw(token);
            if (!TryParseTopLevelProperties(raw, out Dictionary<string, JsonToken> properties, out string parseError))
            {
                AddError(errors, context + " int range 파싱 실패: " + parseError);
                return;
            }

            if (!TryGetProperty(properties, "min", out JsonToken minToken) || !TryReadIntToken(minToken, out int min))
                AddError(errors, context + ".min은 int 값이어야 합니다.");
            if (!TryGetProperty(properties, "max", out JsonToken maxToken) || !TryReadIntToken(maxToken, out int max))
                AddError(errors, context + ".max는 int 값이어야 합니다.");
            if (!TryReadIntToken(minToken, out min) || !TryReadIntToken(maxToken, out max))
                return;

            if (min < minIntValue || max < minIntValue)
                AddError(errors, context + ".min/.max는 둘 다 " + minIntValue.ToString(CultureInfo.InvariantCulture) + " 이상이어야 합니다.");
            if (min > max)
                AddError(errors, context + ".min은 max보다 클 수 없습니다.");
        }

        private static string ReadStringProperty(Dictionary<string, JsonToken> properties, string key)
        {
            if (!TryGetProperty(properties, key, out JsonToken token) || token == null || token.Kind != JsonTokenKind.String)
                return string.Empty;
            return FeatureDescriptorUtility.Normalize(token.StringValue);
        }

        private static bool TryReadIntToken(JsonToken token, out int value)
        {
            value = 0;
            string raw = GetRaw(token);
            return token != null &&
                   token.Kind == JsonTokenKind.Number &&
                   raw.IndexOf('.') < 0 &&
                   raw.IndexOf('e') < 0 &&
                   raw.IndexOf('E') < 0 &&
                   int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetProperty(Dictionary<string, JsonToken> properties, string key, out JsonToken token)
        {
            token = null;
            if (properties == null)
                return false;

            foreach (KeyValuePair<string, JsonToken> pair in properties)
            {
                if (string.Equals(FeatureDescriptorUtility.Normalize(pair.Key), key, StringComparison.Ordinal))
                {
                    token = pair.Value;
                    return true;
                }
            }

            return false;
        }

        private static string GetRaw(JsonToken token)
        {
            return token != null ? token.Raw ?? string.Empty : string.Empty;
        }

        [ThreadStatic] private static string _currentJson;

        private static bool TryParseTopLevelProperties(
            string json,
            out Dictionary<string, JsonToken> properties,
            out string error)
        {
            properties = new Dictionary<string, JsonToken>(StringComparer.Ordinal);
            error = string.Empty;
            string previousJson = _currentJson;
            _currentJson = json ?? string.Empty;
            try
            {
                int index = 0;
                SkipWhitespace(_currentJson, ref index);
                if (index >= _currentJson.Length || _currentJson[index] != '{')
                {
                    error = "JSON object가 아닙니다.";
                    return false;
                }

                index++;
                while (true)
                {
                    SkipWhitespace(_currentJson, ref index);
                    if (index >= _currentJson.Length)
                    {
                        error = "JSON object가 닫히지 않았습니다.";
                        return false;
                    }

                    if (_currentJson[index] == '}')
                    {
                        index++;
                        SkipWhitespace(_currentJson, ref index);
                        if (index != _currentJson.Length)
                        {
                            error = "JSON object 뒤에 추가 문자가 있습니다.";
                            return false;
                        }

                        return true;
                    }

                    if (!TryParseJsonString(_currentJson, ref index, out string key))
                    {
                        error = "JSON object key 문자열 파싱에 실패했습니다.";
                        return false;
                    }

                    SkipWhitespace(_currentJson, ref index);
                    if (index >= _currentJson.Length || _currentJson[index] != ':')
                    {
                        error = "JSON object key '" + key + "' 뒤에 ':'가 없습니다.";
                        return false;
                    }

                    index++;
                    SkipWhitespace(_currentJson, ref index);
                    if (!TryReadJsonToken(_currentJson, ref index, out JsonToken token, out string tokenError))
                    {
                        error = "JSON object key '" + key + "' 값 오류: " + tokenError;
                        return false;
                    }

                    properties[key] = token;
                    SkipWhitespace(_currentJson, ref index);
                    if (index >= _currentJson.Length)
                    {
                        error = "JSON object가 닫히지 않았습니다.";
                        return false;
                    }

                    if (_currentJson[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    if (_currentJson[index] == '}')
                        continue;

                    error = "JSON object 항목 구분자가 올바르지 않습니다.";
                    return false;
                }
            }
            finally
            {
                _currentJson = previousJson;
            }
        }

        private static bool TryReadJsonToken(string json, ref int index, out JsonToken token, out string error)
        {
            token = new JsonToken { Start = index, End = index, Kind = JsonTokenKind.Unknown };
            error = string.Empty;
            if (index >= json.Length)
            {
                error = "값이 없습니다.";
                return false;
            }

            char ch = json[index];
            if (ch == '"')
            {
                int start = index;
                if (!TryParseJsonString(json, ref index, out string value))
                {
                    error = "문자열 값 파싱에 실패했습니다.";
                    return false;
                }

                token.Kind = JsonTokenKind.String;
                token.Start = start;
                token.End = index;
                token.Raw = json.Substring(start, index - start);
                token.StringValue = value;
                return true;
            }

            if (ch == '{')
                return TrySkipComposite(json, ref index, token, JsonTokenKind.Object, '{', '}', out error);
            if (ch == '[')
                return TrySkipComposite(json, ref index, token, JsonTokenKind.Array, '[', ']', out error);
            if (ch == '-' || (ch >= '0' && ch <= '9'))
                return TryReadNumber(json, ref index, token, out error);
            if (TryReadLiteral(json, ref index, "true"))
            {
                token.Kind = JsonTokenKind.Bool;
                token.End = index;
                token.Raw = "true";
                return true;
            }
            if (TryReadLiteral(json, ref index, "false"))
            {
                token.Kind = JsonTokenKind.Bool;
                token.End = index;
                token.Raw = "false";
                return true;
            }
            if (TryReadLiteral(json, ref index, "null"))
            {
                token.Kind = JsonTokenKind.Null;
                token.End = index;
                token.Raw = "null";
                return true;
            }

            error = "지원하지 않는 JSON 값입니다.";
            return false;
        }

        private static bool TrySkipComposite(
            string json,
            ref int index,
            JsonToken token,
            JsonTokenKind kind,
            char open,
            char close,
            out string error)
        {
            error = string.Empty;
            int depth = 0;
            int start = index;
            while (index < json.Length)
            {
                char ch = json[index];
                if (ch == '"')
                {
                    if (!TryParseJsonString(json, ref index, out _))
                    {
                        error = "문자열 파싱에 실패했습니다.";
                        return false;
                    }

                    continue;
                }

                if (ch == open)
                    depth++;
                else if (ch == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        index++;
                        token.Kind = kind;
                        token.Start = start;
                        token.End = index;
                        token.Raw = json.Substring(start, index - start);
                        return true;
                    }
                }

                index++;
            }

            error = "닫는 '" + close + "'가 없습니다.";
            return false;
        }

        private static bool TryReadNumber(string json, ref int index, JsonToken token, out string error)
        {
            error = string.Empty;
            int start = index;
            if (json[index] == '-')
                index++;

            bool hasDigit = false;
            while (index < json.Length && char.IsDigit(json[index]))
            {
                hasDigit = true;
                index++;
            }

            if (index < json.Length && json[index] == '.')
            {
                index++;
                while (index < json.Length && char.IsDigit(json[index]))
                {
                    hasDigit = true;
                    index++;
                }
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                    index++;
                while (index < json.Length && char.IsDigit(json[index]))
                {
                    hasDigit = true;
                    index++;
                }
            }

            if (!hasDigit)
            {
                error = "숫자 형식이 아닙니다.";
                return false;
            }

            token.Kind = JsonTokenKind.Number;
            token.Start = start;
            token.End = index;
            token.Raw = json.Substring(start, index - start);
            return true;
        }

        private static bool TryReadLiteral(string json, ref int index, string literal)
        {
            if (index + literal.Length > json.Length)
                return false;
            for (int i = 0; i < literal.Length; i++)
            {
                if (json[index + i] != literal[i])
                    return false;
            }

            index += literal.Length;
            return true;
        }

        private static bool TryParseJsonString(string json, ref int index, out string value)
        {
            value = string.Empty;
            if (index >= json.Length || json[index] != '"')
                return false;

            var builder = new StringBuilder();
            index++;
            while (index < json.Length)
            {
                char ch = json[index++];
                if (ch == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (ch != '\\')
                {
                    builder.Append(ch);
                    continue;
                }

                if (index >= json.Length)
                    return false;

                char escaped = json[index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (index + 4 > json.Length)
                            return false;
                        string hex = json.Substring(index, 4);
                        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
                            return false;
                        builder.Append((char)codePoint);
                        index += 4;
                        break;
                    default:
                        return false;
                }
            }

            return false;
        }

        private static void SkipWhitespace(string value, ref int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
        }

        private static void AddError(List<string> errors, string message)
        {
            if (errors == null || string.IsNullOrWhiteSpace(message))
                return;
            errors.Add(message);
        }
    }
}

