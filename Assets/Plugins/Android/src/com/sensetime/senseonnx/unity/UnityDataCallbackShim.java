package com.sensetime.senseonnx.unity;

import com.sensetime.senseonnx.DataCallbackListener;

/**
 * Bridge class to allow Unity's AndroidJavaProxy to listen to SenseOnnx abstract class callbacks.
 */
public class UnityDataCallbackShim {
    
    public interface FloatCallback {
        void onBegin();
        void onChunk(float[] data);
        void onEnd();
        void onError(String error);
        void onFinish(float[] data);
    }

    public interface StringCallback {
        void onBegin();
        void onChunk(String data);
        void onEnd();
        void onError(String error);
        void onFinish(String data);
    }

    public static class FloatShim extends DataCallbackListener<float[]> {
        private FloatCallback callback;
        public FloatShim(FloatCallback callback) {
            this.callback = callback;
        }

        @Override public void onBegin() { if (callback != null) callback.onBegin(); }
        @Override public void onChunk(float[] data) { if (callback != null) callback.onChunk(data); }
        @Override public void onEnd() { if (callback != null) callback.onEnd(); }
        @Override public void onError(String error) { if (callback != null) callback.onError(error); }
        @Override public void onFinish(float[] data) { if (callback != null) callback.onFinish(data); }
    }

    public static class StringShim extends DataCallbackListener<String> {
        private StringCallback callback;
        public StringShim(StringCallback callback) {
            this.callback = callback;
        }

        @Override public void onBegin() { if (callback != null) callback.onBegin(); }
        @Override public void onChunk(String data) { if (callback != null) callback.onChunk(data); }
        @Override public void onEnd() { if (callback != null) callback.onEnd(); }
        @Override public void onError(String error) { if (callback != null) callback.onError(error); }
        @Override public void onFinish(String data) { if (callback != null) callback.onFinish(data); }
    }
}
