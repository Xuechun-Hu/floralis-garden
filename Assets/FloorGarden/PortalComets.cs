using UnityEngine;

// Each ribbon has a bright head and a fading tail along a curled, three-dimensional path.
internal sealed class PortalComets
{
    private const int Count = 84, Steps = 18;
    private readonly Transform root;
    private readonly Material ink;

    public PortalComets(PortalArt art, Transform portal)
    {
        var mesh = art.Own(new Mesh { name = "Portal comet ribbons" });
        var vertices = new Vector3[Count * (Steps + 1) * 2];
        var uv = new Vector2[vertices.Length];
        var ids = new Vector2[vertices.Length];
        var triangles = new int[Count * Steps * 6];
        int t = 0;
        for (int i = 0; i < Count; i++)
        {
            for (int j = 0; j <= Steps; j++)
            {
                int a = (i * (Steps + 1) + j) * 2;
                uv[a] = new Vector2((float)j / Steps, -1);
                uv[a + 1] = new Vector2(uv[a].x, 1);
                ids[a] = ids[a + 1] = new Vector2(i, 0);
                if (j == Steps) continue;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = a + 2;
                triangles[t++] = a + 2; triangles[t++] = a + 1; triangles[t++] = a + 3;
            }
        }
        mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = ids; mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 5);
        ink = art.MaterialFor("PortalComets");
        root = PortalArt.Show("Gold and iris comet trails", portal, mesh, ink);
        root.gameObject.SetActive(false);
    }

    public void Tick(float age)
    {
        if (!root.gameObject.activeSelf) root.gameObject.SetActive(true);
        ink.SetFloat("_Age", age);
    }
}
