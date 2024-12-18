namespace SLAU.Common.Models.Results.Base;

[Serializable]
public class BaseResult
{
    public bool IsSuccess { get; set; }
    public string ErrorMessage { get; set; }

    public BaseResult()
    {
        IsSuccess = true;
    }

    public void SetError(string message)
    {
        IsSuccess = false;
        ErrorMessage = message;
    }
}