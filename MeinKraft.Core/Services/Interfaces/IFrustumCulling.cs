public interface IFrustumCulling
{
    ICameraMatrixProvider? CameraMatrix { get; set; }

    void CalcFrustumEquations();
    bool SphereInFrustum(float x, float y, float z, float radius);
}