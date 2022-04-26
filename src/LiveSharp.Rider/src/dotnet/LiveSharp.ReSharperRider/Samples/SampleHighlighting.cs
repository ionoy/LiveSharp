using JetBrains.DocumentModel;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using LiveSharp.ReSharperRider;

[assembly: RegisterConfigurableSeverity(
    SampleHighlighting.SeverityId,
    CompoundItemName: null,
    Group: HighlightingGroupIds.CodeSmell,
    Title: SampleHighlighting.Message,
    Description: SampleHighlighting.Description,
    DefaultSeverity: Severity.WARNING)]

namespace LiveSharp.ReSharperRider
{
    [ConfigurableSeverityHighlighting(
        SeverityId,
        CSharpLanguage.Name,
        OverlapResolve = OverlapResolveKind.ERROR,
        OverloadResolvePriority = 0,
        ToolTipFormatString = Message)]
    public class SampleHighlighting : IHighlighting
    {
        public const string SeverityId = nameof(SampleHighlighting);
        public const string Message = "LiveSharp highlighting message";
        public const string Description = "Samplexxxx highlighting description";
        
        public SampleHighlighting(ICSharpDeclaration declaration)
        {
            Declaration = declaration;
        }

        public ICSharpDeclaration Declaration { get; }

        public bool IsValid()
        {
            return Declaration.IsValid();
        }

        public DocumentRange CalculateRange()
        {
            return Declaration.NameIdentifier?.GetHighlightingRange() ?? DocumentRange.InvalidRange;
        }

        public string ToolTip => Message;
        
        public string ErrorStripeToolTip
            => $"Sample highlighting error tooltip for '{Declaration.DeclaredName}'";
    }
}