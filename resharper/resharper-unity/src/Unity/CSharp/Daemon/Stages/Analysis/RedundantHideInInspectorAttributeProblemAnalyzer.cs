﻿#nullable enable

using JetBrains.Application.Parts;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Errors;
using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration.Api;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Stages.Analysis
{
    [ElementProblemAnalyzer(Instantiation.DemandAnyThreadSafe, typeof(IAttribute),
        HighlightingTypes = new[] { typeof(RedundantHideInInspectorAttributeWarning) })]
    public class RedundantHideInInspectorAttributeProblemAnalyzer : UnityElementProblemAnalyzer<IAttribute>
    {
        public RedundantHideInInspectorAttributeProblemAnalyzer(UnityApi unityApi)
            : base(unityApi)
        {
        }

        protected override void Analyze(IAttribute attribute, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            if (attribute.TypeReference?.Resolve().DeclaredElement is not ITypeElement attributeTypeElement)
                return;

            if (!Equals(attributeTypeElement.GetClrName(), KnownTypes.HideInInspectorAttribute))
                return;

            foreach (var declaration in AttributesOwnerDeclarationNavigator.GetByAttribute(attribute))
            {
                // We must explicitly check the declaration kind, and for properties, the attribute target, as the
                // attribute can technically be applied to all attribute targets. We have a separate analysis for this.
                if ((declaration.DeclaredElement is IField field
                     && Api.IsSerialisedField(field).HasFlag(SerializedFieldStatus.NonSerializedField))
                    || (declaration.DeclaredElement is IProperty property
                        && attribute.Target == AttributeTarget.Field
                        && Api.IsSerialisedAutoProperty(property, true).HasFlag(SerializedFieldStatus.NonSerializedField)))
                {
                    consumer.AddHighlighting(new RedundantHideInInspectorAttributeWarning(attribute));
                    return;
                }
            }
        }
    }
}
