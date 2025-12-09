# 开发文档


### Release 流程

1. 测试各种功能，把要加的功能都加好，Bug都尽量修完。
1. 使用Visual Studio的C#的Release方式，选择打包.net运行时。
1. 增加模型文件夹，default_config.json文件。
1. 打包压缩包后，在其他机器测试。
1. 创建release，上传压缩包到Github Release和Gitee（压缩包分卷，规避单个文件大小的问题。）
