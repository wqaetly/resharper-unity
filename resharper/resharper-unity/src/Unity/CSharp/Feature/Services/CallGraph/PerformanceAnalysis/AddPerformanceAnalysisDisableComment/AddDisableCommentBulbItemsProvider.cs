using System.Collections.Generic;
using JetBrains.Application.Parts;
using JetBrains.Application.UI.Controls.BulbMenu.Items;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Resources;
using JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.CallGraph;
using JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Stages.PerformanceCriticalCodeAnalysis.ContextSystem;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.TextControl;

namespace JetBrains.ReSharper.Plugins.Unity.CSharp.Feature.Services.CallGraph.PerformanceAnalysis.AddPerformanceAnalysisDisableComment
{
    [SolutionComponent(Instantiation.DemandAnyThreadSafe)]
    public sealed class AddDisableCommentBulbItemsProvider : PerformanceCriticalBulbItemsProvider
    {
        public AddDisableCommentBulbItemsProvider(ISolution solution, PerformanceCriticalContextProvider performanceCriticalContextProvider)
            : base(solution, performanceCriticalContextProvider)
        {
        }
        
        protected override IEnumerable<BulbMenuItem> GetActions(IMethodDeclaration methodDeclaration, ITextControl textControl)
        {
            var bulb = new AddPerformanceAnalysisDisableCommentBulbAction(methodDeclaration);
            var bulbMenuItem = UnityCallGraphUtil.BulbActionToMenuItem(bulb, textControl, Solution, BulbThemedIcons.ContextAction.Id);

            return new[] {bulbMenuItem};
        }
    }
}