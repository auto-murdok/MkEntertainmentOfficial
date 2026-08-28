using UnityEngine;
using UnityEngine.Animations.Rigging;

public class RigRecoil
{
    private static readonly Vector3 RecoilDirection = new Vector3(-1, 0, 0);
    private const float RecoilRecoverySpeed = 10f;

    private MultiAimConstraint _ikConstraint;
    private Vector3 _originalConstraintOffset;

    public RigRecoil(MultiAimConstraint ikConstraint)
    {
        _ikConstraint = ikConstraint;
        _originalConstraintOffset = ikConstraint.data.offset;
    }

    public void ApplyRecoil(float kickForce)
    {
        _ikConstraint.data.offset = _originalConstraintOffset + RecoilDirection * kickForce;
    }

    public void RelieveRecoil()
    {
        if (_ikConstraint.data.offset != _originalConstraintOffset)
        {
            _ikConstraint.data.offset = Vector3.Lerp(_ikConstraint.data.offset, _originalConstraintOffset, RecoilRecoverySpeed * Time.deltaTime);
        }
    }
}
