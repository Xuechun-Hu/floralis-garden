using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class GaussianAngel
{
    public readonly Transform Root;
    private readonly Material ink;
    private readonly Mesh mesh;
    private Vector3[] centers;
    private int[] bins, indices;
    private readonly int[] offsets=new int[512];
    private Vector3 lastAxis,sortingAxis;
    private int sortCursor=-1;
    private bool writingOrder;

    [StructLayout(LayoutKind.Sequential)]
    private struct Half4
    {
        public ushort x,y,z,w;
        public Half4(Vector3 v)
        {
            x=Mathf.FloatToHalf(v.x);y=Mathf.FloatToHalf(v.y);z=Mathf.FloatToHalf(v.z);w=0;
        }
    }

    // Interleave attributes in Unity's semantic order, 48 bytes per quad vertex.
    [StructLayout(LayoutKind.Sequential)]
    private struct SplatVertex
    {
        public Vector3 position;
        public Color32 color;
        public ushort cornerX,cornerY;
        public Half4 axisA,axisB,axisC;
        public ushort u,v;
    }
    private const MeshUpdateFlags UploadFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;
    public GaussianAngel(PortalArt art,Transform parent)
    {
        mesh=art.Own(new Mesh{name="SHARP garden angel",indexFormat=IndexFormat.UInt32});
        mesh.MarkDynamic();
        ink=art.MaterialFor("AngelSplats");
        ink.SetTexture("_Portrait",Resources.Load<Texture2D>("GardenAngelDetail1512"));
        Root=PortalArt.Show("芙洛拉 - SHARP",parent,mesh,ink);
    }

    [StructLayout(LayoutKind.Sequential,Pack=4)]
    private struct SourceSplat
    {
        public Vector3 position,axisA,axisB,axisC;
        public Vector2 uv;
        public Color32 color;
    }

    public IEnumerator Prepare(TextAsset data)
    {
        var bytes=data.GetData<byte>();
        var header=bytes.GetSubArray(0,8).Reinterpret<int>(1);
        int count=header[1];
        if(header[0]!=0x3253414c || count<1 || bytes.Length!=8+(long)count*60)
            throw new InvalidDataException("Invalid SHARP splat asset");
        var source=bytes.GetSubArray(8,bytes.Length-8).Reinterpret<SourceSplat>(1);
        centers=new Vector3[count];bins=new int[count];indices=new int[count*6];
        mesh.SetVertexBufferParams(count*4,
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.UNorm8,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float16,2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float16,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float16,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord3,VertexAttributeFormat.Float16,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord4,VertexAttributeFormat.Float16,2));
        mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt32);
        const int chunk=4096;
        var vertices=new SplatVertex[chunk*4];
        ushort one=Mathf.FloatToHalf(1),minusOne=Mathf.FloatToHalf(-1);
        for(int start=0;start<count;start+=chunk)
        {
            int n=Mathf.Min(chunk,count-start);
            for(int j=0;j<n;j++)
            {
                var raw=source[start+j];
                centers[start+j]=raw.position;
                var v=new SplatVertex{position=raw.position,color=raw.color,
                    axisA=new Half4(raw.axisA),axisB=new Half4(raw.axisB),axisC=new Half4(raw.axisC),
                    u=Mathf.FloatToHalf(raw.uv.x),v=Mathf.FloatToHalf(raw.uv.y)};
                for(int c=0;c<4;c++)
                {
                    v.cornerX=(c&1)==0?minusOne:one;v.cornerY=(c&2)==0?minusOne:one;
                    vertices[4*j+c]=v;
                }
                int bin=Mathf.Clamp((int)((raw.position.z+1)*255),0,511);
                bins[start+j]=bin;offsets[bin]++;
            }
            mesh.SetVertexBufferData(vertices,0,start*4,n*4,0,UploadFlags);
            yield return null;
        }
        PrefixOffsets();
        for(int i=0;i<count;i++){WriteIndices(i);if((i&32767)==32767)yield return null;}
        for(int offset=0;offset<indices.Length;offset+=chunk*24)
        {
            int n=Mathf.Min(chunk*24,indices.Length-offset);
            mesh.SetIndexBufferData(indices,offset,offset,n,UploadFlags);
            yield return null;
        }
        lastAxis=Vector3.forward;
        mesh.subMeshCount=1;mesh.bounds=new Bounds(Vector3.up*.3f,Vector3.one*4);
        mesh.SetSubMesh(0,new SubMeshDescriptor(0,indices.Length,MeshTopology.Triangles)
            {bounds=mesh.bounds,vertexCount=count*4},UploadFlags);
        mesh.UploadMeshData(false);
    }

    private void PrefixOffsets()
    {
        int cursor=0;
        for(int b=511;b>=0;b--){int n=offsets[b];offsets[b]=cursor;cursor+=n*6;}
    }
    private void WriteIndices(int i)
    {
        int b=bins[i],k=offsets[b],v=i*4;offsets[b]=k+6;
        indices[k]=v;indices[k+1]=v+2;indices[k+2]=v+1;
        indices[k+3]=v+1;indices[k+4]=v+2;indices[k+5]=v+3;
    }
    public void Sort(Vector3 localAxis)
    {
        if(sortCursor<0)
        {
            if(Vector3.Dot(localAxis,lastAxis)>.995f)return;
            sortingAxis=localAxis;
            sortCursor=0;writingOrder=false;
            System.Array.Clear(offsets,0,offsets.Length);
        }
        // Keep the last complete ordering visible while preparing the next view.
        int end=Mathf.Min(sortCursor+24576,centers.Length);
        if(!writingOrder)
        {
            for(;sortCursor<end;sortCursor++)
            {
                int bin=Mathf.Clamp((int)((Vector3.Dot(sortingAxis,centers[sortCursor])+1)*255),0,511);
                bins[sortCursor]=bin;offsets[bin]++;
            }
            if(sortCursor==centers.Length){PrefixOffsets();sortCursor=0;writingOrder=true;}
            return;
        }
        for(;sortCursor<end;sortCursor++)WriteIndices(sortCursor);
        if(sortCursor<centers.Length)return;
        mesh.SetIndexBufferData(indices,0,0,indices.Length,UploadFlags);
        lastAxis=sortingAxis;
        sortCursor=-1;
    }
    public void Scatter(float elapsed)=>ink.SetFloat("_Scatter",elapsed);
}
