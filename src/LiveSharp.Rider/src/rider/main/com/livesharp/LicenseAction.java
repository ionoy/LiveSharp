package com.livesharp;

import com.intellij.openapi.actionSystem.AnAction;
import com.intellij.openapi.actionSystem.AnActionEvent;
import com.intellij.openapi.actionSystem.PlatformDataKeys;
import com.intellij.openapi.ui.DialogWrapper;
import org.jetbrains.annotations.Nullable;

import javax.swing.*;

public class LicenseAction extends AnAction {

    @Override
    public void actionPerformed(AnActionEvent e) {

        DialogWrapper dialogWrapper = new DialogWrapper(PlatformDataKeys.PROJECT.getData(e.getDataContext())) {
            {
                init();
            }

            @Nullable
            @Override
            protected JComponent createCenterPanel() {

                try {
                    LicenseForm frm = new LicenseForm();
                    JPanel panel = frm.getPanel();
                     return panel;
                } catch (Exception ex) {
                    JPanel panel = new JPanel();
                    JLabel lbl = new JLabel();
                    lbl.setText(ex.getMessage());
                    panel.add(lbl);
                    return panel;
                }
            }
        };

        dialogWrapper.setTitle("LiveSharp");
        dialogWrapper.showAndGet();
    }

    @Override
    public void update(AnActionEvent e) {
        super.update(e);

        //e.getPresentation()
    }



}