#nullable enable
using JetBrains.Application.Parts;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Errors;
using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration.Api;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Stages.Analysis;

[ElementProblemAnalyzer(
    Instantiation.DemandAnyThreadSafe,
    typeof(IEqualityExpression),
    HighlightingTypes =
    [
        typeof(UnityObjectNullComparisonWarning)
    ])]
public class UnityObjectNullComparisonProblemAnalyzer(UnityApi unityApi)
    : UnityElementProblemAnalyzer<IEqualityExpression>(unityApi)
{
    protected override void Analyze(IEqualityExpression expression, ElementProblemAnalyzerData data,
        IHighlightingConsumer consumer)
    {
        var left = expression.LeftOperand.GetOperandThroughParenthesis();
        var right = expression.RightOperand.GetOperandThroughParenthesis();
        if (left == null || right == null)
            return;
        if (left.IsNullLiteral() && UnityTypeUtils.IsUnityObject(right.Type())
             || right.IsNullLiteral() && UnityTypeUtils.IsUnityObject(left.Type()))
        {
            IHighlighting highlighting = new UnityObjectNullComparisonWarning(expression);
            consumer.AddHighlighting(highlighting);
        }
    }
}