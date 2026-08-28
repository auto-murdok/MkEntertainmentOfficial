using UnityEngine;

public interface IObserver<EGenericEnum, EGenericValue>
{
    public void OnNotify(EGenericEnum eGenericEnum, EGenericValue eGenericValue);
}
