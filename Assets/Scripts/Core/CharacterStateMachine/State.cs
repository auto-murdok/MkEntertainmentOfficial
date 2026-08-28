
public interface State<EGenericEnum, EGenericStruct> where EGenericStruct : struct
{
    void EnterState(StateMachine<EGenericEnum, EGenericStruct> character);
    void UpdateState(StateMachine<EGenericEnum, EGenericStruct> character);
    void ExitState(StateMachine<EGenericEnum, EGenericStruct> character);
    void CheckTransitions(StateMachine<EGenericEnum, EGenericStruct> character);
}
