namespace Client.Domain;
public sealed class DuplicatePhoneException : Exception
{
    public DuplicatePhoneException() : base("phone_taken: Телефон уже зарегистрирован") { }
}