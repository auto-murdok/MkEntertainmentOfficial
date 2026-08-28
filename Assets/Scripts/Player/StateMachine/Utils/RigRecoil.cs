using UnityEngine;
using UnityEngine.Animations.Rigging;

public class RigRecoil
{
    private MultiAimConstraint _ikConstraint;
    private Vector3 _originalConstraintOffset;
    private Vector3 _recoilDirection = new Vector3(-1, 0, 0);

    public RigRecoil(MultiAimConstraint ikConstraint)
    {
        _ikConstraint = ikConstraint;
        _originalConstraintOffset = ikConstraint.data.offset;
    }
    public void ApplyRecoil(float kickForce)
    {
        _ikConstraint.data.offset = _originalConstraintOffset + _recoilDirection * kickForce;
    }

    public void RelieveRecoil()
    {
        if (_ikConstraint.data.offset != _originalConstraintOffset)
        {
            _ikConstraint.data.offset = Vector3.Lerp(_ikConstraint.data.offset, _originalConstraintOffset, 10f * Time.deltaTime);
        }
    }
}