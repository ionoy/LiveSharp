package com.livesharp.impl

import com.intellij.application.subscribe
import com.intellij.ide.actions.SaveAllAction
import com.intellij.ide.actions.SaveDocumentAction
import com.intellij.openapi.Disposable
import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.actionSystem.DataContext
import com.intellij.openapi.actionSystem.ex.AnActionListener
import com.intellij.openapi.components.ProjectComponent
import com.intellij.openapi.project.Project
import com.jetbrains.rider.model.liveSharpModel
import com.jetbrains.rider.projectView.solution
import com.jetbrains.rider.util.idea.PsiFile
import com.jetbrains.rider.model.FileEvent

class FileEventsListener(project: Project) : ProjectComponent, Disposable {

    init {
        AnActionListener.TOPIC.subscribe(this, ActionListenerImpl(project))
    }

    class ActionListenerImpl(private val project: Project) : AnActionListener {

        //TODO: File Added, File Removed from fileEditor
        //References?
        override fun afterActionPerformed(action: AnAction, dataContext: DataContext, event: AnActionEvent) {

            if (action is SaveDocumentAction
                    || action is SaveAllAction) {

                val psiFile = event.dataContext.PsiFile ?: return
                val filePath = psiFile.virtualFile.canonicalPath.toString()
                val data = psiFile.containingFile.text
                val dataArray = psiFile.containingFile.textToCharArray()

                var args = FileEvent(
                        filePath,
                        data,
                        dataArray,
                        action.javaClass.toString()
                )

                val model = project.solution.liveSharpModel
                model.fileEvent.fire(args)
            }

            super.afterActionPerformed(action, dataContext, event)
        }
    }

    override fun dispose() {
    }


}

