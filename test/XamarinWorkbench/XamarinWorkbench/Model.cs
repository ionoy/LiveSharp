namespace XamarinWorkbench
{
    public class Model
    {
        private readonly string _name;
        public string Name => ", " + _name;

        public Model(string name)
        {
            _name = name;
        }
    }
}