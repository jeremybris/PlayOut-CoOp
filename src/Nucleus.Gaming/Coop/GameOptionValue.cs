namespace Nucleus.Gaming.Coop
{
    public abstract class GameOptionValue
    {
        private string name;
        private object value;

        public string Name => name;

        public object Value => value;

        public GameOptionValue(string nam, object val)
        {
            name = nam;
            value = val;
        }

        public override string ToString()
        {
            return name;
        }
    }
}
