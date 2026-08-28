using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IStore
{
    public void UpdateContext<EGenericScopedContext>(EGenericScopedContext context);
}
