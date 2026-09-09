namespace LogThis.Entities
{
    public static class LogThisHelper
    {
        public static NamedArg[] AddMessageComponents(params object[] objects)
        {
            NamedArg[] messageComponents = new NamedArg[objects.Length];

            for (int i = 0; i < objects.Length; i++)
            {
                messageComponents[i] = new NamedArg(objects[i]);
            }

            return messageComponents;
        }
    }
}
