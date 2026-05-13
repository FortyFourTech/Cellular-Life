using System;
using UnityEngine;
using UnityEngine.Rendering;

public abstract class PingPongResource<T> : IDisposable {
    protected T _resourceA;
    protected T _resourceB;
    protected bool _isA;
    protected string _paramName;

    public T ReadResource => _isA ? _resourceA : _resourceB;
    public T WriteResource => _isA ? _resourceB : _resourceA;

    protected PingPongResource(T resA, T resB, string paramName)
    {
        _resourceA = resA;
        _resourceB = resB;
        _paramName = paramName;
    }

    protected PingPongResource(string paramName)
    {
        _paramName = paramName;
    }

    public abstract void Dispose();

    public virtual void SwapResource(CommandBuffer cb = null)
    {
        _isA = !_isA;
        SetResources(cb);
    }
    public abstract void Sync(CommandBuffer cb = null);

    public abstract void SetResources(CommandBuffer cb = null);
    public abstract void SetResources(ComputeShader cs, int kernelIdx);
}

public class PingPongBuffer : PingPongResource<GraphicsBuffer>
{
    public PingPongBuffer(int count, int stride, string paramName)
        : base(paramName)
    {
        _resourceA = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.CopyDestination | GraphicsBuffer.Target.CopySource, count, stride);
        _resourceB = new GraphicsBuffer(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.CopyDestination | GraphicsBuffer.Target.CopySource, count, stride);
    }

    public override void Dispose()
    {
        _resourceA.Release();
        _resourceB.Release();
    }

    public override void SetResources(CommandBuffer cb = null)
    {
        if (cb != null)
        {
            cb.SetGlobalBuffer(_paramName+"RW", ReadResource);
            cb.SetGlobalBuffer(_paramName+"RO", ReadResource);
            cb.SetGlobalBuffer(_paramName+"WO", WriteResource);
        }
        else
        {
            Shader.SetGlobalBuffer(_paramName+"RW", ReadResource);
            Shader.SetGlobalBuffer(_paramName+"RO", ReadResource);
            Shader.SetGlobalBuffer(_paramName+"WO", WriteResource);
        }
    }
    public override void SetResources(ComputeShader cs, int kernelIdx)
    {
        cs.SetBuffer(kernelIdx, _paramName+"RW", ReadResource);
        cs.SetBuffer(kernelIdx, _paramName+"RO", ReadResource);
        cs.SetBuffer(kernelIdx, _paramName+"WO", WriteResource);
    }
    public override void Sync(CommandBuffer cb = null)
    {
        if (cb != null)
        {
            cb.CopyBuffer(WriteResource, ReadResource);
        }
        else
        {
            Graphics.CopyBuffer(WriteResource, ReadResource);
        }
    }
}
public class PingPongTexture : PingPongResource<RenderTexture>
{
    public PingPongTexture(int width, int height, RenderTextureFormat format, string paramName)
        : base(paramName)
    {
        _resourceA = new RenderTexture(width, height, 0, format)
        {
            enableRandomWrite = true
        };
        _resourceA.Create();
        Graphics.SetRenderTarget(_resourceA);
        GL.Clear(false, true, Color.black);

        _resourceB = new RenderTexture(_resourceA);
        _resourceB.Create();
        Graphics.SetRenderTarget(_resourceB);
        GL.Clear(false, true, Color.black);
    }

    public override void Dispose()
    {
        _resourceA.Release();
        _resourceB.Release();
    }

    public override void SetResources(CommandBuffer cb = null)
    {
        if (cb != null)
        {
            cb.SetGlobalTexture(_paramName+"RW", ReadResource);
            cb.SetGlobalTexture(_paramName+"RO", ReadResource);
            cb.SetGlobalTexture(_paramName+"WO", WriteResource);
        }
        else
        {
            Shader.SetGlobalTexture(_paramName+"RW", ReadResource);
            Shader.SetGlobalTexture(_paramName+"RO", ReadResource);
            Shader.SetGlobalTexture(_paramName+"WO", WriteResource);
        }
    }
    public override void SetResources(ComputeShader cs, int kernelIdx)
    {
        cs.SetTexture(kernelIdx, _paramName+"RW", ReadResource);
        cs.SetTexture(kernelIdx, _paramName+"RO", ReadResource);
        cs.SetTexture(kernelIdx, _paramName+"WO", WriteResource);
    }
    public override void Sync(CommandBuffer cb = null)
    {
        if (cb != null)
        {
            cb.CopyTexture(WriteResource, ReadResource);
        }
        else
        {
            Graphics.CopyTexture(WriteResource, ReadResource);
        }
    }
}
