using System;
using System.Linq.Expressions;

namespace LiveSharp.RuntimeTests.Helpers
{
    public static class GenericMethods
    {
        public static void OneWayBind<TViewModel, TView, TVMProp, TVProp>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TVMProp>> vmProperty,
            Expression<Func<TView, TVProp>> viewProperty,
            object conversionHint = null,
            object vmToViewConverterOverride = null)
            where TViewModel : class
            where TView : class
        {
            
        }
        
        public static void OneWayBind<TViewModel, TView, TProp, TOut>(
            this TView view,
            TViewModel viewModel,
            Expression<Func<TViewModel, TProp>> vmProperty,
            Expression<Func<TView, TOut>> viewProperty,
            Func<TProp, TOut> selector)
            where TViewModel : class
            where TView : class
        {
            
        }
    }
}