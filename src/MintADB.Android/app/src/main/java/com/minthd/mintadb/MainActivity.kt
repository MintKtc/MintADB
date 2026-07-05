package com.minthd.mintadb

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import com.minthd.mintadb.ui.MintAdbApp
import com.minthd.mintadb.ui.theme.MintAdbTheme
import rikka.shizuku.Shizuku

class MainActivity : ComponentActivity() {
    private val viewModel: MintAdbViewModel by viewModels()
    private val requestCode = 1001

    private val permissionListener =
        Shizuku.OnRequestPermissionResultListener { requestCode, grantResult ->
            if (requestCode == this.requestCode) {
                viewModel.onShizukuPermissionResult(grantResult == android.content.pm.PackageManager.PERMISSION_GRANTED)
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Shizuku.addRequestPermissionResultListener(permissionListener)
        setContent {
            MintAdbTheme {
                MintAdbApp(
                    viewModel = viewModel,
                    onRequestShizuku = { viewModel.requestShizukuPermission(requestCode) },
                )
            }
        }
    }

    override fun onDestroy() {
        Shizuku.removeRequestPermissionResultListener(permissionListener)
        super.onDestroy()
    }
}