// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Xml.XPath;
using System.Xml.Xsl.Xslt;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace System.Xml.Xsl.Runtime
{
    // List of all XPath/XSLT runtime methods
    internal static class XsltMethods
    {
        // Formatting error messages
        public static readonly MethodInfo FormatMessage = typeof(XsltLibrary).GetMethod("FormatMessage")!;

        // Runtime type checks and casts
        public static readonly MethodInfo EnsureNodeSet = typeof(XsltConvert).GetMethod("EnsureNodeSet", new[] { typeof(IList<XPathItem>) })!;

        // Comparisons
        public static readonly MethodInfo EqualityOperator = typeof(XsltLibrary).GetMethod("EqualityOperator")!;
        public static readonly MethodInfo RelationalOperator = typeof(XsltLibrary).GetMethod("RelationalOperator")!;

        // XPath functions
        public static readonly MethodInfo StartsWith = typeof(XsltFunctions).GetMethod("StartsWith")!;
        public static readonly MethodInfo Contains = typeof(XsltFunctions).GetMethod("Contains")!;
        public static readonly MethodInfo SubstringBefore = typeof(XsltFunctions).GetMethod("SubstringBefore")!;
        public static readonly MethodInfo SubstringAfter = typeof(XsltFunctions).GetMethod("SubstringAfter")!;
        public static readonly MethodInfo Substring2 = typeof(XsltFunctions).GetMethod("Substring", new[] { typeof(string), typeof(double) })!;
        public static readonly MethodInfo Substring3 = typeof(XsltFunctions).GetMethod("Substring", new[] { typeof(string), typeof(double), typeof(double) })!;
        public static readonly MethodInfo NormalizeSpace = typeof(XsltFunctions).GetMethod("NormalizeSpace")!;
        public static readonly MethodInfo Translate = typeof(XsltFunctions).GetMethod("Translate")!;
        public static readonly MethodInfo Lang = typeof(XsltFunctions).GetMethod("Lang")!;
        public static readonly MethodInfo Floor = typeof(Math).GetMethod("Floor", new[] { typeof(double) })!;
        public static readonly MethodInfo Ceiling = typeof(Math).GetMethod("Ceiling", new[] { typeof(double) })!;
        public static readonly MethodInfo Round = typeof(XsltFunctions).GetMethod("Round")!;

        // XSLT functions and helper methods (static)
        public static readonly MethodInfo SystemProperty = typeof(XsltFunctions).GetMethod("SystemProperty")!;
        public static readonly MethodInfo BaseUri = typeof(XsltFunctions).GetMethod("BaseUri")!;
        public static readonly MethodInfo OuterXml = typeof(XsltFunctions).GetMethod("OuterXml")!;
        public static readonly MethodInfo OnCurrentNodeChanged = typeof(XmlQueryRuntime).GetMethod("OnCurrentNodeChanged")!;

        // MSXML extension functions
        public static readonly MethodInfo MSFormatDateTime = typeof(XsltFunctions).GetMethod("MSFormatDateTime")!;
        public static readonly MethodInfo MSStringCompare = typeof(XsltFunctions).GetMethod("MSStringCompare")!;
        public static readonly MethodInfo MSUtc = typeof(XsltFunctions).GetMethod("MSUtc")!;
        public static readonly MethodInfo MSNumber = typeof(XsltFunctions).GetMethod("MSNumber")!;
        public static readonly MethodInfo MSLocalName = typeof(XsltFunctions).GetMethod("MSLocalName")!;
        public static readonly MethodInfo MSNamespaceUri = typeof(XsltFunctions).GetMethod("MSNamespaceUri")!;

        // EXSLT functions
        public static readonly MethodInfo EXslObjectType = typeof(XsltFunctions).GetMethod("EXslObjectType")!;

        // XSLT functions and helper methods (non-static)
        public static readonly MethodInfo CheckScriptNamespace = typeof(XsltLibrary).GetMethod("CheckScriptNamespace")!;
        public static readonly MethodInfo FunctionAvailable = GetFunctionAvailableMethod();
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "Supressing warning about not having the RequiresUnreferencedCode attribute since this code path " +
            "will only be emitting IL that will later be called by Transform() method which is already annotated as RequiresUnreferencedCode")]
        private static MethodInfo GetFunctionAvailableMethod() => typeof(XsltLibrary).GetMethod("FunctionAvailable")!;
        public static readonly MethodInfo ElementAvailable = typeof(XsltLibrary).GetMethod("ElementAvailable")!;
        public static readonly MethodInfo RegisterDecimalFormat = typeof(XsltLibrary).GetMethod("RegisterDecimalFormat")!;
        public static readonly MethodInfo RegisterDecimalFormatter = typeof(XsltLibrary).GetMethod("RegisterDecimalFormatter")!;
        public static readonly MethodInfo FormatNumberStatic = typeof(XsltLibrary).GetMethod("FormatNumberStatic")!;
        public static readonly MethodInfo FormatNumberDynamic = typeof(XsltLibrary).GetMethod("FormatNumberDynamic")!;
        public static readonly MethodInfo IsSameNodeSort = typeof(XsltLibrary).GetMethod("IsSameNodeSort")!;
        public static readonly MethodInfo LangToLcid = typeof(XsltLibrary).GetMethod("LangToLcid")!;
        public static readonly MethodInfo NumberFormat = typeof(XsltLibrary).GetMethod("NumberFormat")!;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class XsltLibrary
    {
        private readonly XmlQueryRuntime _runtime;
        private HybridDictionary? _functionsAvail;
        private Dictionary<XmlQualifiedName, DecimalFormat>? _decimalFormats;
        private List<DecimalFormatter>? _decimalFormatters;

        internal XsltLibrary(XmlQueryRuntime runtime)
        {
            _runtime = runtime;
        }

        public string FormatMessage(string res, IList<string> args)
        {
            string[] arr = new string[args.Count];

            for (int i = 0; i < arr.Length; i++)
                arr[i] = args[i];

            return XslTransformException.CreateMessage(res, arr);
        }

        public int CheckScriptNamespace(string nsUri)
        {
            // Check that extension and script namespaces do not clash
            if (_runtime.ExternalContext.GetLateBoundObject(nsUri) != null)
            {
                throw new XslTransformException(SR.Xslt_ScriptAndExtensionClash, nsUri);
            }
            return 0;   // have to return something
        }

        // Spec: http://www.w3.org/TR/xslt#function-element-available
        public bool ElementAvailable(XmlQualifiedName name)
        {
            return QilGenerator.IsElementAvailable(name);
        }

        // Spec: http://www.w3.org/TR/xslt#function-function-available
        [RequiresUnreferencedCode(Scripts.ExtensionFunctionCannotBeStaticallyAnalyzed)]
        public bool FunctionAvailable(XmlQualifiedName name)
        {
            if (_functionsAvail == null)
            {
                _functionsAvail = new HybridDictionary();
            }
            else
            {
                object? obj = _functionsAvail[name];
                if (obj != null)
                {
                    return (bool)obj;
                }
            }

            bool result = FunctionAvailableHelper(name);
            _functionsAvail[name] = result;
            return result;
        }

        [RequiresUnreferencedCode(Scripts.ExtensionFunctionCannotBeStaticallyAnalyzed)]
        private bool FunctionAvailableHelper(XmlQualifiedName name)
        {
            // Is this an XPath or an XSLT function?
            if (QilGenerator.IsFunctionAvailable(name.Name, name.Namespace))
            {
                return true;
            }

            // Script blocks and extension objects cannot implement neither null nor XSLT namespace
            if (name.Namespace.Length == 0 || name.Namespace == XmlReservedNs.NsXslt)
            {
                return false;
            }

            // Is this an extension object function?
            if (_runtime.ExternalContext.LateBoundFunctionExists(name.Name, name.Namespace))
            {
                return true;
            }

            // Is this a script function?
            return _runtime.EarlyBoundFunctionExists(name.Name, name.Namespace);
        }

        public int RegisterDecimalFormat(XmlQualifiedName name, string infinitySymbol, string nanSymbol, string characters)
        {
            _decimalFormats ??= new Dictionary<XmlQualifiedName, DecimalFormat>();
            _decimalFormats.Add(name, CreateDecimalFormat(infinitySymbol, nanSymbol, characters));
            return 0;   // have to return something
        }

        private static DecimalFormat CreateDecimalFormat(string infinitySymbol, string nanSymbol, string characters)
        {
            // BUGBUG: Fallback to the old XSLT implementation
            NumberFormatInfo info = new NumberFormatInfo();
            info.NumberDecimalSeparator = char.ToString(characters[0]);
            info.NumberGroupSeparator = char.ToString(characters[1]);
            info.PositiveInfinitySymbol = infinitySymbol;
            info.NegativeSign = char.ToString(characters[7]);
            info.NaNSymbol = nanSymbol;
            info.PercentSymbol = char.ToString(characters[2]);
            info.PerMilleSymbol = char.ToString(characters[3]);
            info.NegativeInfinitySymbol = info.NegativeSign + info.PositiveInfinitySymbol;

            return new DecimalFormat(info, characters[5], characters[4], characters[6]);
        }

        public double RegisterDecimalFormatter(string formatPicture, string infinitySymbol, string nanSymbol, string characters)
        {
            _decimalFormatters ??= new List<DecimalFormatter>();
            _decimalFormatters.Add(new DecimalFormatter(formatPicture, CreateDecimalFormat(infinitySymbol, nanSymbol, characters)));
            return _decimalFormatters.Count - 1;
        }

        public string FormatNumberStatic(double value, double decimalFormatterIndex)
        {
            int idx = (int)decimalFormatterIndex;
            Debug.Assert(_decimalFormatters != null && 0 <= idx && idx < _decimalFormatters.Count, "Value of decimalFormatterIndex is out of range");
            return _decimalFormatters[idx].Format(value);
        }

        public string FormatNumberDynamic(double value, string formatPicture, XmlQualifiedName decimalFormatName, string errorMessageName)
        {
            DecimalFormat? format;
            if (_decimalFormats == null || !_decimalFormats.TryGetValue(decimalFormatName, out format))
            {
                throw new XslTransformException(SR.Xslt_NoDecimalFormat, errorMessageName);
            }

            DecimalFormatter formatter = new DecimalFormatter(formatPicture, format);
            return formatter.Format(value);
        }

        public string NumberFormat(IList<XPathItem> value, string formatString,
            double lang, string letterValue, string groupingSeparator, double groupingSize)
        {
            // REVIEW: For each execution of xsl:number new Format() object is created.
            // Then there is no AVTs we can build this object once and reuse it later to improve performance.
            NumberFormatter formatter = new NumberFormatter(formatString, (int)lang, letterValue, groupingSeparator, (int)groupingSize);
            return formatter.FormatSequence(value);
        }

        internal const int InvariantCultureLcid = 0x007f;

        public int LangToLcid(string lang, bool forwardCompatibility)
        {
            return LangToLcidInternal(lang, forwardCompatibility, null);
        }

        internal static int LangToLcidInternal(string lang, bool forwardCompatibility, IErrorHelper? errorHelper)
        {
            int lcid = InvariantCultureLcid;

            if (lang != null)
            {
                // The value of the 'lang' attribute must be a non-empty nmtoken
                if (lang.Length == 0)
                {
                    if (!forwardCompatibility)
                    {
                        if (errorHelper != null)
                        {
                            errorHelper.ReportError(SR.Xslt_InvalidAttrValue, nameof(lang), lang);
                        }
                        else
                        {
                            throw new XslTransformException(SR.Xslt_InvalidAttrValue, nameof(lang), lang);
                        }
                    }
                }
                else
                {
                    // Check if lang is a supported culture name
                    try
                    {
                        lcid = new CultureInfo(lang).LCID;
                    }
                    catch (ArgumentException)
                    {
                        if (!forwardCompatibility)
                        {
                            if (errorHelper != null)
                            {
                                errorHelper.ReportError(SR.Xslt_InvalidLanguage, lang);
                            }
                            else
                            {
                                throw new XslTransformException(SR.Xslt_InvalidLanguage, lang);
                            }
                        }
                    }
                }
            }
            return lcid;
        }

        internal const string InvariantCultureName = "";

        internal static string LangToNameInternal(string lang, bool forwardCompatibility, IErrorHelper errorHelper)
        {
            string cultName = InvariantCultureName;

            if (lang != null)
            {
                // The value of the 'lang' attribute must be a non-empty nmtoken
                if (lang.Length == 0)
                {
                    if (!forwardCompatibility)
                    {
                        if (errorHelper != null)
                        {
                            errorHelper.ReportError(/*[XT_032]*/SR.Xslt_InvalidAttrValue, nameof(lang), lang);
                        }
                        else
                        {
                            throw new XslTransformException(SR.Xslt_InvalidAttrValue, nameof(lang), lang);
                        }
                    }
                }
                else
                {
                    // Check if lang is a supported culture name
                    try
                    {
                        cultName = new CultureInfo(lang).Name;
                    }
                    catch (System.ArgumentException)
                    {
                        if (!forwardCompatibility)
                        {
                            if (errorHelper != null)
                            {
                                errorHelper.ReportError(/*[XT_033]*/SR.Xslt_InvalidLanguage, lang);
                            }
                            else
                            {
                                throw new XslTransformException(SR.Xslt_InvalidLanguage, lang);
                            }
                        }
                    }
                }
            }
            return cultName;
        }

        #region Comparisons
        internal enum ComparisonOperator
        {
            /*Equality  */
            Eq, Ne,
            /*Relational*/
            Lt, Le, Gt, Ge,
        }

        // Returns TypeCode of the given atomic value
        private static TypeCode GetTypeCode(XPathItem item)
        {
            // Faster implementation of Type.GetTypeCode(item.ValueType);
            Debug.Assert(!item.IsNode, "Atomic value expected");
            Type itemType = item.ValueType;
            if (itemType == XsltConvert.StringType)
            {
                return TypeCode.String;
            }
            else if (itemType == XsltConvert.DoubleType)
            {
                return TypeCode.Double;
            }
            else
            {
                Debug.Assert(itemType == XsltConvert.BooleanType, $"Unexpected type of atomic value {itemType}");
                return TypeCode.Boolean;
            }
        }

        // Returns weakest of the two given TypeCodes, String > Double > Boolean
        private static TypeCode WeakestTypeCode(TypeCode typeCode1, TypeCode typeCode2)
        {
            Debug.Assert(TypeCode.Boolean < TypeCode.Double && TypeCode.Double < TypeCode.String, "Cannot use the smallest TypeCode as a weakest one");
            return typeCode1 < typeCode2 ? typeCode1 : typeCode2;
        }

        private static bool CompareNumbers(ComparisonOperator op, double left, double right) =>
            op switch
            {
                ComparisonOperator.Eq => left == right,
                ComparisonOperator.Ne => left != right,
                ComparisonOperator.Lt => left < right,
                ComparisonOperator.Le => left <= right,
                ComparisonOperator.Gt => left > right,
                _ => left >= right,
            };

        private static bool CompareValues(ComparisonOperator op, XPathItem left, XPathItem right, TypeCode compType)
        {
            if (compType == TypeCode.Double)
            {
                return CompareNumbers(op, XsltConvert.ToDouble(left), XsltConvert.ToDouble(right));
            }
            else
            {
                Debug.Assert(op == ComparisonOperator.Eq || op == ComparisonOperator.Ne);
                if (compType == TypeCode.String)
                {
                    return (XsltConvert.ToString(left) == XsltConvert.ToString(right)) == (op == ComparisonOperator.Eq);
                }
                else
                {
                    Debug.Assert(compType == TypeCode.Boolean);
                    return (XsltConvert.ToBoolean(left) == XsltConvert.ToBoolean(right)) == (op == ComparisonOperator.Eq);
                }
            }
        }

        private static bool CompareNodeSetAndValue(ComparisonOperator op, IList<XPathNavigator> nodeset, XPathItem val, TypeCode compType)
        {
            Debug.Assert(compType == TypeCode.Boolean || compType == TypeCode.Double || compType == TypeCode.String);
            if (compType == TypeCode.Boolean)
            {
                // Cast nodeset to boolean type, then take its ordinal number
                return CompareNumbers(op, (nodeset.Count != 0) ? 1 : 0, XsltConvert.ToBoolean(val) ? 1 : 0);
            }
            else
            {
                int length = nodeset.Count;
                for (int idx = 0; idx < length; idx++)
                {
                    if (CompareValues(op, nodeset[idx], val, compType))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private static bool CompareNodeSetAndNodeSet(ComparisonOperator op, IList<XPathNavigator> left, IList<XPathNavigator> right, TypeCode compType)
        {
            int leftLen = left.Count;
            int rightLen = right.Count;
            for (int leftIdx = 0; leftIdx < leftLen; leftIdx++)
            {
                for (int rightIdx = 0; rightIdx < rightLen; rightIdx++)
                {
                    if (CompareValues(op, left[leftIdx], right[rightIdx], compType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool EqualityOperator(double opCode, IList<XPathItem> left, IList<XPathItem> right)
        {
            ComparisonOperator op = (ComparisonOperator)opCode;
            Debug.Assert(op == ComparisonOperator.Eq || op == ComparisonOperator.Ne);
            CheckXsltValue(left);
            CheckXsltValue(right);

            if (IsNodeSetOrRtf(left))
            {
                if (IsNodeSetOrRtf(right))
                {
                    // Both left and right are node-sets
                    return CompareNodeSetAndNodeSet(op, ToNodeSetOrRtf(left), ToNodeSetOrRtf(right), TypeCode.String);
                }
                else
                {
                    // left is a node-set, right is an atomic value
                    XPathItem rightItem = right[0];
                    return CompareNodeSetAndValue(op, ToNodeSetOrRtf(left), rightItem, GetTypeCode(rightItem));
                }
            }
            else if (IsNodeSetOrRtf(right))
            {
                // left is an atomic value, right is a node-set
                XPathItem leftItem = left[0];
                // Swap operands:  left op right  ->  right op left
                return CompareNodeSetAndValue(op, ToNodeSetOrRtf(right), leftItem, GetTypeCode(leftItem));
            }
            else
            {
                // Both left and right are atomic values
                XPathItem leftItem = left[0];
                XPathItem rightItem = right[0];
                return CompareValues(op, leftItem, rightItem, WeakestTypeCode(GetTypeCode(leftItem), GetTypeCode(rightItem)));
            }
        }

        // Inverts relational operator in order to swap operands of the comparison
        private static ComparisonOperator InvertOperator(ComparisonOperator op) =>
            op switch
            {
                ComparisonOperator.Lt => ComparisonOperator.Gt,
                ComparisonOperator.Le => ComparisonOperator.Ge,
                ComparisonOperator.Gt => ComparisonOperator.Lt,
                ComparisonOperator.Ge => ComparisonOperator.Le,
                _ => op,
            };

        public bool RelationalOperator(double opCode, IList<XPathItem> left, IList<XPathItem> right)
        {
            ComparisonOperator op = (ComparisonOperator)opCode;
            Debug.Assert(ComparisonOperator.Lt <= op && op <= ComparisonOperator.Ge);
            CheckXsltValue(left);
            CheckXsltValue(right);

            if (IsNodeSetOrRtf(left))
            {
                if (IsNodeSetOrRtf(right))
                {
                    // Both left and right are node-sets
                    return CompareNodeSetAndNodeSet(op, ToNodeSetOrRtf(left), ToNodeSetOrRtf(right), TypeCode.Double);
                }
                else
                {
                    // left is a node-set, right is an atomic value
                    XPathItem rightItem = right[0];
                    return CompareNodeSetAndValue(op, ToNodeSetOrRtf(left), rightItem, WeakestTypeCode(GetTypeCode(rightItem), TypeCode.Double));
                }
            }
            else if (IsNodeSetOrRtf(right))
            {
                // left is an atomic value, right is a node-set
                XPathItem leftItem = left[0];
                // Swap operands:  left op right  ->  right InvertOperator(op) left
                op = InvertOperator(op);
                return CompareNodeSetAndValue(op, ToNodeSetOrRtf(right), leftItem, WeakestTypeCode(GetTypeCode(leftItem), TypeCode.Double));
            }
            else
            {
                // Both left and right are atomic values
                XPathItem leftItem = left[0];
                XPathItem rightItem = right[0];
                return CompareValues(op, leftItem, rightItem, TypeCode.Double);
            }
        }
        #endregion

        // nav1 and nav2 are assumed to belong to the same document
        public bool IsSameNodeSort(XPathNavigator nav1, XPathNavigator nav2)
        {
            Debug.Assert(XPathNodeType.SignificantWhitespace == XPathNodeType.Text + 1);
            Debug.Assert(XPathNodeType.Whitespace == XPathNodeType.Text + 2);

            XPathNodeType nt1 = nav1.NodeType;
            XPathNodeType nt2 = nav2.NodeType;

            // If one of nodes is a text node, the other one must also be a text node
            if (XPathNodeType.Text <= nt1 && nt1 <= XPathNodeType.Whitespace)
            {
                return XPathNodeType.Text <= nt2 && nt2 <= XPathNodeType.Whitespace;
            }

            // Otherwise nodes must have the same node kind, the same local name, and the same namespace URI
            Debug.Assert((object)nav1.NameTable == (object)nav2.NameTable, "Ref.Equal cannot be used if navigators have different name tables");
            return nt1 == nt2 && Ref.Equal(nav1.LocalName, nav2.LocalName) && Ref.Equal(nav1.NamespaceURI, nav2.NamespaceURI);
        }


        //------------------------------------------------
        // Helper methods
        //------------------------------------------------

        [Conditional("DEBUG")]
        internal static void CheckXsltValue(XPathItem item)
        {
            CheckXsltValue(new XmlQueryItemSequence(item));
        }

        [Conditional("DEBUG")]
        internal static void CheckXsltValue(IList<XPathItem> val)
        {
            // IsDocOrderDistinct is not always set to true even if the node-set is ordered
            // Debug.Assert(val.Count <= 1 || val.IsDocOrderDistinct, "All node-sets must be ordered");

            if (val.Count == 1)
            {
                XsltFunctions.EXslObjectType(val);
            }
            else
            {
                // Every item must be a node, but for performance reasons we check only
                // the first two and the last two items
                int count = val.Count;
                for (int idx = 0; idx < count; idx++)
                {
                    if (!val[idx].IsNode)
                    {
                        Debug.Fail("Invalid XSLT value");
                        break;
                    }
                    if (idx == 1)
                    {
                        idx += Math.Max(count - 4, 0);
                    }
                }
            }
        }

        private static bool IsNodeSetOrRtf(IList<XPathItem> val)
        {
            CheckXsltValue(val);
            if (val.Count == 1)
            {
                return val[0].IsNode;
            }
            return true;
        }

        private static IList<XPathNavigator> ToNodeSetOrRtf(IList<XPathItem> val)
        {
            return XmlILStorageConverter.ItemsToNavigators(val);
        }
    }
}
