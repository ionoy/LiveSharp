using System;
using System.ComponentModel;
using System.Linq;
using Xamarin.Forms;

namespace LiveSharp.Support.XamarinForms
{
    public class XamarinFormsInspector : ILiveSharpInspector
    {
        private readonly ILiveSharpRuntime _runtime;

        private INotifyPropertyChanged _currentBindingContext;
        private ActionDisposable _currentBindingContextDisposable;
        private ActionDisposable _currentPageSubscription;
        private InstanceInspector _currentInstanceInspector;

        public XamarinFormsInspector(ILiveSharpRuntime runtime)
        {
            _runtime = runtime;
        }

        public void Render()
        {
            if (_currentInstanceInspector != null)
                UpdatePanel(_currentInstanceInspector.Serialize());
            else if (_currentBindingContext != null)
                UpdatePanel(new InstanceInspector(_currentBindingContext, _runtime.Logger).Serialize());
        }
        
        private void OnCurrentPageChanged(Page page)
        {
            _currentPageSubscription?.Dispose();

            if (page == null)
                return;

            page.BindingContextChanged += bindingContextChanged;

            _currentPageSubscription = new ActionDisposable(() => page.BindingContextChanged -= bindingContextChanged);

            Device.BeginInvokeOnMainThread(() =>
            {
                object bindingContext = null;
                try {
                    bindingContext = page.BindingContext;
                } catch {
                    // Page._properties might not be initialized yet, so BindingContext throws NRE 
                }

                if (bindingContext != null)
                    OnCurrentPageBindingContextChanged(bindingContext);
            });
            
            void bindingContextChanged(object sender, EventArgs e)
            {
                OnCurrentPageBindingContextChanged(page.BindingContext);
            }
        }
        
        
        public void SetCurrentContext(object context)
        {
            if (context is ContentPage contentPage) {
                OnCurrentPageChanged(contentPage);
            }
        }
        
        private void OnCurrentPageBindingContextChanged(object bindingContext)
        {
            if (bindingContext is INotifyPropertyChanged inpc)
            {
                if (_currentBindingContext != inpc)
                {
                    _currentBindingContextDisposable?.Dispose();

                    _currentInstanceInspector = new InstanceInspector(inpc, _runtime.Logger);

                    inpc.PropertyChanged += inpcPropertyChanged;

                    _currentBindingContext = inpc;
                    _currentBindingContextDisposable = new ActionDisposable(() => inpc.PropertyChanged -= inpcPropertyChanged);

                    void inpcPropertyChanged(object sender, PropertyChangedEventArgs e)
                    {
                        var existingInspector = _currentInstanceInspector.Properties.FirstOrDefault(pi => pi.PropertyInfo.Name == e.PropertyName);                        

                        if (existingInspector != null)
                        {
                            existingInspector.UpdateValue(inpc);
                        }
                        else
                        {
                            // If property was added without our knowledge (dynamic stuff)
                            var properties = InstanceInspector.GetAllProperties(inpc);
                            var property = properties.FirstOrDefault(p => p.Name == e.PropertyName);

                            if (property != null)
                                _currentInstanceInspector.Properties.Add(new PropertyInspector(property, inpc, _runtime.Logger));
                        }

                        UpdatePanel(_currentInstanceInspector.Serialize());
                    }
                    
                    UpdatePanel(_currentInstanceInspector.Serialize());
                }
            }
        }

        private void UpdatePanel(string serializedInstance)
        {
            _runtime.UpdateDiagnosticPanel("Inspector", serializedInstance);
        }
    }
}
