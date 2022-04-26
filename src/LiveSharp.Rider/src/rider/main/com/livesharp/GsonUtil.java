package com.livesharp;

import com.google.gson.*;

import java.io.InputStreamReader;
import java.util.List;
import java.lang.reflect.Type;


public class GsonUtil {

    private static final Gson gson = new GsonBuilder()
            .setDateFormat("yyyy-MM-dd'T'hh:mm:ss")
            .create();

    public static String serializeToJSON(Object obj) {
        return gson.toJson(obj);
    }

    public static <T> Object deserializeFromReader(InputStreamReader json, Class<T> classType) {
        return gson.fromJson(json, classType);
    }

    public static <T> Object deserializeFromJSON(String json, Class<T> classType) {
        return gson.fromJson(json, classType);
    }

    public static <T> List<T> deserializeListFromJSON(String json, Type listType) {
        return gson.fromJson(json, listType);
    }
    public static <T> Object deserializeListFromReader(InputStreamReader json, Type listType) {
        return gson.fromJson(json, listType);
    }

}
