package com.livesharp;

import com.intellij.openapi.components.ServiceManager;
import com.intellij.openapi.project.Project;
import org.jetbrains.annotations.NotNull;


public interface ProjectService {



    static ProjectService getInstance(@NotNull Project project) {

        //project.getProjectFile()

// when it's no longer needed, call 'connection.disconnect()'

        return ServiceManager.getService(project, ProjectService.class);
    }
}
