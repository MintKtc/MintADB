package com.minthd.mintadb;

interface IShellService {
    void destroy() = 16777114;
    void exit() = 1;
    String run(String command) = 2;
}