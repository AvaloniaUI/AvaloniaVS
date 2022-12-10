namespace CompletionEngineTests.Models
{
    public class GenericBaseClass<T>
    {
        public T GenericProperty { get; set; }
    }

    public class EmptyClassDerivedFromGenericClassWithDouble : GenericBaseClass<double>
    {
        
    }
}