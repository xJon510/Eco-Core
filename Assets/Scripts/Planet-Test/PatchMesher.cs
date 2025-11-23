using UnityEngine;

public static class PatchMesher
{
    // Build a grid patch over uvRect on one cube face, then spherify
    public static Mesh BuildPatchMesh(PlanetController.CubeFace face, Rect uvRect, float radius, int res)
    {
        int vertsPerSide = res + 1;
        int vertCount = vertsPerSide * vertsPerSide;
        int quadCount = res * res;
        int triCount = quadCount * 2;

        Vector3[] vtx = new Vector3[vertCount];
        Vector3[] nrm = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];
        int[] idx = new int[triCount * 3];

        int vi = 0;
        for (int y = 0; y <= res; y++)
        {
            float v = Mathf.Lerp(uvRect.yMin, uvRect.yMax, (float)y / res);
            for (int x = 0; x <= res; x++)
            {
                float u = Mathf.Lerp(uvRect.xMin, uvRect.xMax, (float)x / res);
                Vector3 p = Evaluate(face, new Vector2(u, v), radius);
                vtx[vi] = p;
                nrm[vi] = p.normalized;
                uv[vi] = new Vector2((u + 1f) * 0.5f, (v + 1f) * 0.5f);
                vi++;
            }
        }

        int ii = 0;
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                int a = y * (res + 1) + x;
                int b = a + 1;
                int c = a + (res + 1);
                int d = c + 1;
                // two tris: a,c,b  and  b,c,d
                idx[ii++] = a; idx[ii++] = c; idx[ii++] = b;
                idx[ii++] = b; idx[ii++] = c; idx[ii++] = d;
            }
        }

        Mesh m = new Mesh();
        m.indexFormat = (vertCount > 65000) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.vertices = vtx;
        m.normals = nrm;
        m.uv = uv;
        m.triangles = idx;
        m.RecalculateBounds();
        return m;
    }

    // Map (u,v in [-1,1]) on a cube face to a point on the sphere
    public static Vector3 Evaluate(PlanetController.CubeFace face, Vector2 uv, float radius)
    {
        Vector3 c = face switch
        {
            PlanetController.CubeFace.PosX => new Vector3(1f, uv.x, uv.y),
            PlanetController.CubeFace.NegX => new Vector3(-1f, uv.x, uv.y),
            PlanetController.CubeFace.PosY => new Vector3(uv.x, 1f, uv.y),
            PlanetController.CubeFace.NegY => new Vector3(uv.x, -1f, uv.y),
            PlanetController.CubeFace.PosZ => new Vector3(uv.x, uv.y, 1f),
            PlanetController.CubeFace.NegZ => new Vector3(uv.x, uv.y, -1f),
            _ => Vector3.zero
        };
        c.Normalize();
        return c * radius;
    }

    // Rough world-space edge length of this patch (for SSE)
    public static float EdgeLength(PlanetController.CubeFace face, Rect uvRect, float radius)
    {
        Vector3 p0 = Evaluate(face, new Vector2(uvRect.xMin, uvRect.yMin), radius);
        Vector3 p1 = Evaluate(face, new Vector2(uvRect.xMax, uvRect.yMin), radius);
        Vector3 p2 = Evaluate(face, new Vector2(uvRect.xMin, uvRect.yMax), radius);
        float a = Vector3.Distance(p0, p1);
        float b = Vector3.Distance(p0, p2);
        return Mathf.Max(a, b);
    }
}
