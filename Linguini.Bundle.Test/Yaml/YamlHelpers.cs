﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Threading;
using Linguini.Syntax.Ast;
using YamlDotNet.RepresentationModel;

namespace Linguini.Bundle.Test.Yaml
{
    public static class YamlHelpers
    {
        #region ROBUSTLY_STOLEN

        // To fetch nodes by key name with YAML, we NEED a YamlScalarNode.
        // We use a thread local one to avoid allocating one every fetch, since we just replace the inner value.
        // Obviously thread local to avoid threading issues.
        private static readonly ThreadLocal<YamlScalarNode> FetchNode =
            new(() => new YamlScalarNode());

        [Pure]
        public static bool TryGetNode<T>(this YamlMappingNode mapping, string key,
            [NotNullWhen(true)] out T? returnNode) where T : YamlNode
        {
            if (mapping.Children.TryGetValue(_getFetchNode(key), out var node))
            {
                returnNode = (T) node;
                return true;
            }

            returnNode = null;
            return false;
        }

        private static YamlScalarNode _getFetchNode(string key)
        {
            var node = FetchNode.Value!;
            node.Value = key;
            return node;
        }

        [Pure]
        public static bool AsBool(this YamlNode node)
        {
            return bool.Parse(node.AsString());
        }

        [Pure]
        public static string AsString(this YamlNode node)
        {
            return ((YamlScalarNode) node).Value ?? "";
        }

        #endregion

        #region TEST_PARSING

        public static List<ResolverTestSuite> ParseResolverTests(this YamlDocument doc)
        {
            var suites = (YamlSequenceNode) doc.RootNode["suites"][0]["suites"];
            var testSuites = new List<ResolverTestSuite>(suites.Children.Count);
            foreach (var suiteProp in suites.Children)
            {
                var testSuite = new ResolverTestSuite();
                if (suiteProp.TryConvert(out YamlMappingNode mapNode))
                {
                    if (mapNode.TryGetNode<YamlScalarNode>("name", out var name))
                    {
                        testSuite.Name = name.Value!;
                    }

                    if (mapNode.TryGetNode<YamlSequenceNode>("resources", out var resources))
                    {
                        ProcessResources(resources, testSuite);
                    }

                    if (mapNode.TryGetNode<YamlSequenceNode>("bundles", out var bundles))
                    {
                        ProcessBundles(bundles, out testSuite.Bundle);
                    }

                    if (mapNode.TryGetNode<YamlSequenceNode>("tests", out var tests))
                    {
                        testSuite.Tests = ProcessTests(tests);
                    }
                }

                testSuites.Add(testSuite);
            }

            return testSuites;
        }

        private static List<ResolverTestSuite.ResolverTest> ProcessTests(YamlSequenceNode testsNode)
        {
            var testCollection = new List<ResolverTestSuite.ResolverTest>();
            foreach (var test in testsNode.Children)
            {
                testCollection.Add(ProcessTest((YamlMappingNode) test));
            }

            return testCollection;
        }

        private static ResolverTestSuite.ResolverTest ProcessTest(YamlMappingNode test)
        {
            var resolverTest = new ResolverTestSuite.ResolverTest();
            if (test.TryGetNode("name", out YamlScalarNode nameProp))
            {
                resolverTest.TestName = nameProp.Value!;
            }

            if (test.TryGetNode("skip", out YamlScalarNode skipProp))
            {
                resolverTest.Skip = skipProp.AsBool();
            }

            if (test.TryGetNode("asserts", out YamlSequenceNode asserts))
            {
                resolverTest.Asserts = ProcessAsserts(asserts);
            }

            if (test.TryGetNode("bundles", out YamlSequenceNode bundleNodes))
            {
                ProcessBundles(bundleNodes, out resolverTest.Bundle);
            }

            return resolverTest;
        }

        private static List<ResolverTestSuite.ResolverAssert> ProcessAsserts(YamlSequenceNode assertsNodes)
        {
            var retVal = new List<ResolverTestSuite.ResolverAssert>();
            foreach (var assertNode in assertsNodes.Children)
            {
                if (assertNode.TryConvert(out YamlMappingNode assertMap))
                {
                    var resolverAssert = new ResolverTestSuite.ResolverAssert();

                    if (assertMap.TryGetNode("id", out YamlScalarNode idNode))
                    {
                        resolverAssert.Id = idNode.Value!;
                    }

                    if (assertMap.TryGetNode("attribute", out YamlScalarNode attrNode))
                    {
                        resolverAssert.Attribute = attrNode.Value;
                    }

                    if (assertMap.TryGetNode("value", out YamlScalarNode valNode))
                    {
                        resolverAssert.ExpectedValue = valNode.AsString();
                    }

                    if (assertMap.TryGetNode("missing", out YamlScalarNode missingNode))
                    {
                        resolverAssert.Missing = missingNode.AsBool();
                    }

                    if (assertMap.TryGetNode("args", out YamlMappingNode argsNode))
                    {
                        resolverAssert.Args = ProcessArgs(argsNode);
                    }

                    if (assertMap.TryGetNode("errors", out YamlSequenceNode errorsNode))
                    {
                        resolverAssert.ExpectedErrors = ProcessErrors(errorsNode);
                    }


                    retVal.Add(resolverAssert);
                }
            }

            return retVal;
        }

        private static Dictionary<string, string> ProcessArgs(YamlMappingNode argsNode)
        {
            var processArgs = new Dictionary<string, string>();
            foreach (var arg in argsNode.Children)
            {
                var key = (YamlScalarNode) arg.Key;
                var val = (YamlScalarNode) arg.Value;
                processArgs.Add(key.Value!, val.Value!);
            }

            return processArgs;
        }

        private static void ProcessBundles(YamlSequenceNode bundles,
            out ResolverTestSuite.ResolverTestBundle? testBundle)
        {
            testBundle = new ResolverTestSuite.ResolverTestBundle();
            foreach (var bundleNode in bundles.Children)
            {
                if (bundleNode.TryConvert(out YamlMappingNode bundleMap))
                {
                    foreach (var keyValueNode in bundleMap.Children)
                    {
                        if (keyValueNode.Key.ToString().Equals("functions"))
                        {
                            ProcessFunctions((YamlSequenceNode) keyValueNode.Value, out testBundle.Functions);
                        }
                        else if (keyValueNode.Key.ToString().Equals("errors"))
                        {
                            testBundle.Errors = ProcessErrors((YamlSequenceNode) bundleMap["errors"]);
                        }
                    }
                }
            }
        }

        private static void ProcessFunctions(YamlSequenceNode functionsNode, out List<string> bundle)
        {
            bundle = new List<string>(functionsNode.Children.Count);
            foreach (var function in functionsNode)
            {
                if (function.TryConvert(out YamlScalarNode funcName))
                {
                    bundle.Add(funcName.Value!);
                }
            }
        }

        private static void ProcessResources(YamlSequenceNode returnNode, ResolverTestSuite testSuite)
        {
            foreach (var resNode in returnNode.Children)
            {
                if (resNode.TryConvert(out YamlMappingNode map))
                {
                    if (map.TryGetNode("source", out YamlScalarNode sourceValue))
                    {
                        testSuite.Resources = sourceValue.Value!;
                    }

                    if (map.TryGetNode("errors", out YamlSequenceNode errorNode))
                    {
                        if (testSuite.Bundle == null)
                        {
                            testSuite.Bundle = new ResolverTestSuite.ResolverTestBundle();
                        }

                        testSuite.Bundle.Errors = ProcessErrors(errorNode);
                    }
                }
            }
        }

        private static List<ResolverTestSuite.ResolverTestError> ProcessErrors(YamlSequenceNode errorNode)
        {
            List<ResolverTestSuite.ResolverTestError> resolverTestErrors = new();
            foreach (var error in errorNode.Children)
            {
                if (error.TryConvert(out YamlMappingNode errMap))
                {
                    var err = new ResolverTestSuite.ResolverTestError();
                    if (errMap.TryGetNode("type", out YamlScalarNode errType))
                    {
                        Enum.TryParse(errType.Value, out err.Type);
                    }

                    if (errMap.TryGetNode("desc", out YamlScalarNode errDesc))
                    {
                        err.Description = errDesc.Value;
                    }

                    resolverTestErrors.Add(err);
                }
            }

            return resolverTestErrors;
        }
        #endregion
    }


}
