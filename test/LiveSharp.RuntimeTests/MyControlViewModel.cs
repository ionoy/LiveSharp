namespace LiveSharp.RuntimeTests
{
    internal class MyControlViewModel
    {
        public MyControlViewModel(int a, string b)
        {
        }

        public MyControlViewModel(int a)
        {
        }


        public object RegistrationPrompt { get; internal set; }
        public object RegistrationCodeValidationMessage { get; internal set; }
        public object RegistrationCode { get; internal set; }
    }
}